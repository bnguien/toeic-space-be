namespace ToeicSpace.Assessment.Infrastructure.Services.Caching;

public sealed class ContentCacheOptions
{
    public const string SectionName = "ContentCache";

    public int ExpirationMinutes { get; init; } = 10;
}
