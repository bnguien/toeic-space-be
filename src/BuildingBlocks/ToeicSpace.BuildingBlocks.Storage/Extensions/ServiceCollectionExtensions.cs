using Amazon.Runtime;
using Amazon.S3;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.BuildingBlocks.Storage.Abstractions;
using ToeicSpace.BuildingBlocks.Storage.CloudflareR2;

namespace ToeicSpace.BuildingBlocks.Storage.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddCloudflareR2Storage(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<CloudflareR2Options>(
            configuration.GetSection(CloudflareR2Options.SectionName));

        var options = configuration
                          .GetSection(CloudflareR2Options.SectionName)
                          .Get<CloudflareR2Options>()
                      ?? throw new InvalidOperationException(
                          "CloudflareR2 configuration is missing.");

        var credentials = new BasicAWSCredentials(
            options.AccessKey,
            options.SecretKey);

        var s3Config = new AmazonS3Config
        {
            ServiceURL = options.Endpoint,
            ForcePathStyle = true
        };

        services.AddSingleton<IAmazonS3>(
            new AmazonS3Client(credentials, s3Config));

        services.AddScoped<IObjectStorageService, R2ObjectStorageService>();

        return services;
    }
}