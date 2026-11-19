using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Content.API.RateLimiting;

/// <summary>
/// Per-user (JWT <c>sub</c>, else client IP) fixed one-minute windows of the video endpoints. Rejections use the
/// shared 429 ProblemDetails (<c>rate_limited</c>) configured by <see cref="VocabularyRateLimits"/>.
/// </summary>
public static class VideoRateLimits
{
    public const string Catalog = "video-catalog";
    public const string Resolve = "video-resolve";
    public const string Subtitles = "video-subtitles";
    public const string Progress = "video-progress";
    public const string Exercise = "video-exercise";
    public const string Words = "video-words";
    public const string Unplayable = "video-unplayable";

    public static IServiceCollection AddVideoRateLimits(this IServiceCollection services) =>
        services.Configure<RateLimiterOptions>(options =>
        {
            Add(options, Catalog, 120);
            Add(options, Resolve, 30);
            Add(options, Subtitles, 60);
            Add(options, Progress, 30);
            Add(options, Exercise, 60);
            Add(options, Words, 60);
            Add(options, Unplayable, 10);
        });

    private static void Add(RateLimiterOptions options, string policy, int permitsPerMinute) =>
        options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0,
                AutoReplenishment = true
            }));
}
