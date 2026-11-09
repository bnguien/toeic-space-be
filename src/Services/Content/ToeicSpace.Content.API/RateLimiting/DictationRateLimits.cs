using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Content.API.RateLimiting;

/// <summary>
/// Per-user (JWT <c>sub</c>, else client IP) fixed windows for the dictation endpoints that reveal content or call
/// the speech service, matching Studychill's per-minute throttles: check 30, hint 60, skeleton 80, sentence audio
/// 80, shared speech 60. Rejections reuse the 429 <c>rate_limited</c> response of <see cref="VocabularyRateLimits"/>.
/// </summary>
public static class DictationRateLimits
{
    public const string Check = "dictation-check";
    public const string Hint = "dictation-hint";
    public const string Skeleton = "dictation-skeleton";
    public const string Audio = "dictation-audio";
    public const string Speech = "speech";

    public static IServiceCollection AddDictationRateLimits(this IServiceCollection services) =>
        services.Configure<RateLimiterOptions>(options =>
        {
            Add(options, Check, 30);
            Add(options, Hint, 60);
            Add(options, Skeleton, 80);
            Add(options, Audio, 80);
            Add(options, Speech, 60);
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
