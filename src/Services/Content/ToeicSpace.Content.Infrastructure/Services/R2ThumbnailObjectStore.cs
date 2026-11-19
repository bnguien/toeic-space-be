using System.Net;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ToeicSpace.BuildingBlocks.Storage.Abstractions;
using ToeicSpace.BuildingBlocks.Storage.CloudflareR2;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Infrastructure.Services;

public sealed class R2ThumbnailObjectStore(IObjectStorageService presigner, IAmazonS3 client,
    IOptions<CloudflareR2Options> options) : IThumbnailObjectStore
{
    private readonly string _bucket = options.Value.BucketName;

    public async Task<string> PresignAsync(string key, string contentType, TimeSpan expiration, CancellationToken ct)
    {
        try { return await presigner.GenerateUploadUrlAsync(key, contentType, expiration, ct); }
        catch (AmazonS3Exception ex)
        { throw new ContentRuleException("storage_unavailable", "Thumbnail storage is temporarily unavailable.", 503, ex); }
    }

    public async Task<StoredThumbnail?> HeadAsync(string key, CancellationToken ct)
    {
        try
        {
            var head = await client.GetObjectMetadataAsync(new GetObjectMetadataRequest
            {
                BucketName = _bucket, Key = key
            }, ct);
            // Content-Type metadata alone is client-controlled; verify the file signature too.
            using var response = await client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucket, Key = key, ByteRange = new ByteRange(0, 11)
            }, ct);
            var bytes = new byte[12];
            var count = 0;
            while (count < bytes.Length)
            {
                var read = await response.ResponseStream.ReadAsync(bytes.AsMemory(count), ct);
                if (read == 0) break;
                count += read;
            }
            var valid = HasValidSignature(head.Headers.ContentType, bytes.AsSpan(0, count));
            return new StoredThumbnail(head.Headers.ContentType, head.ContentLength, valid);
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
        catch (AmazonS3Exception ex)
        {
            throw new ContentRuleException("storage_unavailable", "Thumbnail storage is temporarily unavailable.", 503, ex);
        }
    }

    internal static bool HasValidSignature(string contentType, ReadOnlySpan<byte> bytes) => contentType switch
    {
        "image/jpeg" => bytes.Length >= 3 && bytes[0] == 0xff && bytes[1] == 0xd8 && bytes[2] == 0xff,
        "image/png" => bytes.Length >= 8 && bytes[..8].SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }),
        "image/webp" => bytes.Length >= 12 && bytes[..4].SequenceEqual("RIFF"u8) && bytes.Slice(8, 4).SequenceEqual("WEBP"u8),
        _ => false
    };

    public async Task DeleteAsync(string key, CancellationToken ct)
    {
        await client.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);
    }
}
