using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Content.API.RateLimiting;

/// <summary>
/// Per-user fixed-window limits (JWT <c>sub</c>, else client IP) of the news endpoints: reads 120/min, learner
/// writes and dictation 90/min, guest lookup 60/min. Rejections use the shared 429 <c>rate_limited</c> response
/// configured by <see cref="VocabularyRateLimits"/>. Covers are not limited (static, cached by browsers).
/// </summary>
public static class NewsRateLimits
{
    public const string Read = "news-read";
    public const string Write = "news-write";
    public const string Lookup = "news-lookup";

    public static IServiceCollection AddNewsRateLimits(this IServiceCollection services) =>
        services.Configure<RateLimiterOptions>(options =>
        {
            Add(options, Read, 120);
            Add(options, Write, 90);
            Add(options, Lookup, 60);
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
