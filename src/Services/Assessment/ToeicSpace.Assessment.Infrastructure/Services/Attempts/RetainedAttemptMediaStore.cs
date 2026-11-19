using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using System.Security.Cryptography;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using ToeicSpace.Assessment.Application.Common.Exceptions;
using ToeicSpace.Assessment.Application.Interfaces.Storage;
using ToeicSpace.Assessment.Application.Interfaces.Caching;
using ToeicSpace.Assessment.Domain.Exceptions;

namespace ToeicSpace.Assessment.Infrastructure.Services.Attempts;

// Content-addressed shared objects; no deletion method is exposed to the Attempt lifecycle.
// Only explicitly configured origins may be fetched, including every redirect target.
public sealed class RetainedAttemptMediaStore(IConfiguration config,
    ILogger<RetainedAttemptMediaStore> logger, IContentCache cache) : IAttemptMediaStore
{
    private const long MaximumBytes = 25 * 1024 * 1024;
    public const string UrlPrefix = "/assessment/api/v1/practice-media/";
    private string Bucket => !string.IsNullOrWhiteSpace(config["CloudflareR2:BucketName"])
        ? config["CloudflareR2:BucketName"]! : throw new PracticeMediaException("storage_bucket_missing");
    private AmazonS3Client Client()
    {
        var endpoint = config["CloudflareR2:Endpoint"];
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length != 0)
            throw new PracticeMediaException("storage_endpoint_invalid");
        if (string.IsNullOrWhiteSpace(config["CloudflareR2:AccessKey"])
            || string.IsNullOrWhiteSpace(config["CloudflareR2:SecretKey"]))
            throw new PracticeMediaException("storage_credentials_missing");
        return new(new BasicAWSCredentials(config["CloudflareR2:AccessKey"], config["CloudflareR2:SecretKey"]),
            new AmazonS3Config { ServiceURL = endpoint, ForcePathStyle = true,
                Timeout = TimeSpan.FromSeconds(30), MaxErrorRetry = 1 });
    }

    public async Task<IReadOnlyDictionary<string, string>> PreserveAsync(IEnumerable<string> urls, CancellationToken ct)
    {
        var result = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);
        var sources = urls.Distinct(StringComparer.Ordinal).ToArray();
        if (sources.Length == 0) return result;
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false,
            AutomaticDecompression = DecompressionMethods.All };
        using var http = new HttpClient(handler) { Timeout = Timeout.InfiniteTimeSpan };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("ToeicSpace-Assessment/1.0");
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(BudgetFor(sources.Length));
        var watch = Stopwatch.StartNew();
        try
        {
            logger.LogInformation("Preparing {Count} practice media references", sources.Length);
            var policy = new AttemptMediaSourcePolicy(config.GetSection("PracticeMedia:AllowedOrigins").Get<string[]>() ?? []);
            var bucket = Bucket;
            using var s3 = Client();
            await Parallel.ForEachAsync(sources, new ParallelOptions {
                MaxDegreeOfParallelism = 16, CancellationToken = budget.Token
            }, async (url, cancellationToken) =>
            {
                // This deadline includes headers, body transfer, hashing and storage operations.
                using var transfer = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                transfer.CancelAfter(TimeSpan.FromSeconds(30));
                var token = transfer.Token;
                var reference = AttemptMediaSourcePolicy.Reference(url);
                try
                {
                    var uri = policy.Validate(url);
                    var cacheKey = "assessment:practice-media:v1:" + AttemptMediaSourcePolicy.Reference(
                        config["CloudflareR2:Endpoint"] + "\n" + bucket + "\n" + uri.AbsoluteUri);
                    var retained = await cache.GetAsync<RetainedMediaSource>(cacheKey, token);
                    if (retained != null)
                    {
                        if (!IsHash(retained.Hash) || !IsHash(retained.FinalSourceHash)
                            || (retained.ETag == null && !retained.LastModified.HasValue)) retained = null;
                        else
                        {
                            try { await s3.GetObjectMetadataAsync(bucket, "practice-retained/" + retained.Hash, token); }
                            catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { retained = null; }
                        }
                    }
                    using var response = await policy.GetAsync(http, uri, retained, token);
                    if (response.StatusCode == HttpStatusCode.NotModified && retained != null)
                    {
                        result[url] = UrlPrefix + retained.Hash;
                        return;
                    }
                    if (!response.IsSuccessStatusCode) throw new PracticeMediaException($"source_http_{(int)response.StatusCode}");
                    if (response.StatusCode == HttpStatusCode.PartialContent)
                        throw new PracticeMediaException("incomplete_source_media");
                    if (response.Content.Headers.ContentLength > MaximumBytes) throw new PracticeMediaException("media_too_large");
                    await using var input = await response.Content.ReadAsStreamAsync(token);
                    // Seekable temporary files keep concurrent 25 MiB downloads out of API memory.
                    // DeleteOnClose also removes partial files when a request fails or is cancelled.
                    await using var bytes = new FileStream(Path.Combine(Path.GetTempPath(), Path.GetRandomFileName()),
                        FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 81920,
                        FileOptions.Asynchronous | FileOptions.DeleteOnClose);
                    using var digest = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                    var buffer = new byte[81920];
                    var prefix = new byte[4096];
                    var prefixLength = 0;
                    int read;
                    while ((read = await input.ReadAsync(buffer.AsMemory(), token)) != 0)
                    {
                        if (bytes.Length + read > MaximumBytes) throw new PracticeMediaException("media_too_large");
                        var captured = Math.Min(read, prefix.Length - prefixLength);
                        buffer.AsSpan(0, captured).CopyTo(prefix.AsSpan(prefixLength));
                        prefixLength += captured;
                        digest.AppendData(buffer, 0, read);
                        await bytes.WriteAsync(buffer.AsMemory(0, read), token);
                    }
                    if (bytes.Length == 0) throw new PracticeMediaException("empty_media");
                    // CDN MIME labels are unreliable. Derive the type from recognized media bytes,
                    // even when an upstream labels an HTML/error response as audio or an image.
                    var type = AttemptMediaContentTypes.Detect(prefix.AsSpan(0, prefixLength));
                    if (type == null) throw new PracticeMediaException("unrecognized_media_content");
                    var hash = Convert.ToHexString(digest.GetHashAndReset()).ToLowerInvariant();
                    var key = "practice-retained/" + hash;
                    var upload = false;
                    try
                    {
                        var metadata = await s3.GetObjectMetadataAsync(bucket, key, token);
                        upload = metadata.Headers.ContentType != type;
                    }
                    catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
                    { upload = true; }
                    if (upload)
                    {
                        bytes.Position = 0;
                        // R2 does not implement AWS streaming SigV4/checksum trailers.
                        // HTTPS protects unsigned payloads; HTTP-compatible endpoints still sign the body.
                        await s3.PutObjectAsync(new PutObjectRequest { BucketName = bucket, Key = key,
                            InputStream = bytes, ContentType = type, AutoCloseStream = false,
                            UseChunkEncoding = false, DisableDefaultChecksumValidation = true,
                            DisablePayloadSigning = new Uri(config["CloudflareR2:Endpoint"]!).Scheme == Uri.UriSchemeHttps }, token);
                        await s3.GetObjectMetadataAsync(bucket, key, token);
                    }
                    // Existing shared objects already passed HEAD; newly copied ones are verified above.
                    result[url] = UrlPrefix + hash;
                    // Revalidate upstream before reuse. Never treat a mutable source URL as immutable.
                    var etag = response.Headers.ETag is { IsWeak: false } tag ? tag.ToString() : null;
                    var modified = response.Content.Headers.LastModified;
                    if (etag != null || modified.HasValue)
                    {
                        try
                        {
                            await cache.SetAsync(cacheKey, new RetainedMediaSource(hash,
                                AttemptMediaSourcePolicy.Reference(response.RequestMessage!.RequestUri!.AbsoluteUri),
                                etag, modified), token);
                        }
                        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                        { /* Cache expiry must not invalidate a successfully retained object. */ }
                    }
                }
                // ForEachAsync cancels its other workers after the first fault. Those are
                // cancellations, not independent source timeouts. A per-transfer deadline
                // still reaches the failure handler because only transfer.Token expires.
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    var reason = FailureReason(ex);
                    logger.LogWarning("Practice media source {SourceReference} failed ({Reason})", reference[..12], reason);
                    throw new PracticeMediaException(reason, reference[..12]);
                }
            });
            logger.LogInformation("Preserved {Count} practice media references in {ElapsedMs}ms", sources.Length, watch.ElapsedMilliseconds);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex)
        {
            var reason = FailureReason(ex);
            logger.LogWarning("Practice media preparation failed for {Count} references after {ElapsedMs}ms ({Reason})",
                sources.Length, watch.ElapsedMilliseconds, reason);
            if (ex is PracticeMediaException) throw;
            throw new PracticeMediaException(reason); // Never leak signed URLs, storage credentials or upstream bodies.
        }
        return result;
    }

    public async Task<(Stream Content, string ContentType)> OpenAsync(string hash, CancellationToken ct)
    {
        if (!IsHash(hash)) throw AppException.NotFound("Media unavailable.");
        try
        {
            using var s3 = Client();
            using var response = await s3.GetObjectAsync(Bucket, "practice-retained/" + hash.ToLowerInvariant(), ct);
            var type = AttemptMediaContentTypes.Normalize(response.Headers.ContentType)
                ?? throw AppException.NotFound("Media unavailable.");
            if (response.ContentLength > MaximumBytes) throw AppException.NotFound("Media unavailable.");
            var bytes = new MemoryStream();
            try
            {
                await response.ResponseStream.CopyToAsync(bytes, ct);
                bytes.Position = 0;
                return (bytes, type);
            }
            catch
            {
                await bytes.DisposeAsync();
                throw;
            }
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        { throw AppException.NotFound("Media unavailable."); }
    }

    /// <summary>
    /// Total time allowed for one preparation. Small collections keep the original 75-second budget; a full test
    /// (about 100–150 files, 16 in parallel) gets one extra second per file beyond 50, capped at 4 minutes.
    /// Each transfer keeps its own 30-second deadline, so the request is always bounded.
    /// </summary>
    public static TimeSpan BudgetFor(int sourceCount)
        => TimeSpan.FromSeconds(Math.Clamp(75 + Math.Max(0, sourceCount - 50), 75, 240));

    private static bool IsHash(string? hash) => hash is { Length: 64 } && hash.All(char.IsAsciiHexDigit);

    private static string FailureReason(Exception exception) => exception switch
    {
        PracticeMediaException failure => failure.Reason,
        OperationCanceledException => "media_timeout",
        AmazonS3Exception storage => $"storage_http_{(int)storage.StatusCode}",
        AmazonClientException => "storage_request_failed",
        HttpRequestException => "source_request_failed",
        _ => "media_processing_failed"
    };
}
