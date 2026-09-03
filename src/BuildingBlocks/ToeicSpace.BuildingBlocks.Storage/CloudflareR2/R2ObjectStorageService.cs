using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using ToeicSpace.BuildingBlocks.Storage.Abstractions;

namespace ToeicSpace.BuildingBlocks.Storage.CloudflareR2;

public class R2ObjectStorageService : IObjectStorageService
{
    private readonly IAmazonS3 _s3Client;
    private readonly CloudflareR2Options _options;

    public R2ObjectStorageService(
        IAmazonS3 s3Client,
        IOptions<CloudflareR2Options> options)
    {
        _s3Client = s3Client;
        _options = options.Value;
    }

    public Task<string> GenerateUploadUrlAsync(
        string objectKey,
        string contentType,
        TimeSpan expiration,
        CancellationToken cancellationToken = default)
    {
        var request = new GetPreSignedUrlRequest
        {
            BucketName = _options.BucketName,
            Key = objectKey,
            Verb = HttpVerb.PUT,
            Expires = DateTime.UtcNow.Add(expiration),
            ContentType = contentType
        };

        var url = _s3Client.GetPreSignedURL(request);

        return Task.FromResult(url);
    }
}