using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ToeicSpace.BuildingBlocks.Storage.Abstractions;
using ToeicSpace.BuildingBlocks.Storage.CloudflareR2;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Infrastructure.Services;

public sealed class R2LessonMediaStore(IObjectStorageService presigner, IAmazonS3 client,
    IOptions<CloudflareR2Options> options) : ILessonMediaStore
{
    private readonly string _bucket = options.Value.BucketName;

    public Task<string> PresignUploadAsync(string key, string contentType, TimeSpan lifetime, CancellationToken ct) =>
        presigner.GenerateUploadUrlAsync(key, contentType, lifetime, ct);

    public Task<string> PresignReadAsync(string key, TimeSpan lifetime, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        return Task.FromResult(client.GetPreSignedURL(new GetPreSignedUrlRequest
        {
            BucketName = _bucket, Key = key, Verb = HttpVerb.GET,
            Expires = DateTime.UtcNow.Add(lifetime)
        }));
    }

    public async Task<StoredThumbnail?> HeadAsync(string key, CancellationToken ct)
    {
        try
        {
            var head = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            { BucketName = _bucket, Key = key }, ct);
            using var response = await client.GetObjectAsync(new GetObjectRequest
            { BucketName = _bucket, Key = key, ByteRange = new ByteRange(0, 11) }, ct);
            var bytes = new byte[12];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = await response.ResponseStream.ReadAsync(bytes.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            var signature = HasValidSignature(head.Headers.ContentType, bytes.AsSpan(0, count));
            return new StoredThumbnail(head.Headers.ContentType, head.ContentLength, signature);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound) { return null; }
        catch (AmazonS3Exception ex)
        { throw new ContentRuleException("storage_unavailable", "Media storage is temporarily unavailable.", 503, ex); }
    }

    internal static bool HasValidSignature(string contentType, ReadOnlySpan<byte> bytes) => contentType switch
    {
        "video/mp4" => bytes.Length >= 8 && bytes.Slice(4, 4).SequenceEqual("ftyp"u8),
        "application/pdf" => bytes.Length >= 4 && bytes[..4].SequenceEqual("%PDF"u8),
        _ => R2ThumbnailObjectStore.HasValidSignature(contentType, bytes)
    };

    public async Task DeleteAsync(string key, CancellationToken ct) =>
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);
}
