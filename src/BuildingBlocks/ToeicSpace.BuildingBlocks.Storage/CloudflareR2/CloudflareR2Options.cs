namespace ToeicSpace.BuildingBlocks.Storage.CloudflareR2;

public class CloudflareR2Options
{
    public const string SectionName = "CloudflareR2";

    public string Endpoint { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;
}