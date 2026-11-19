using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Content.API.RateLimiting;

/// <summary>
/// Site-wide "select to look up" limit: 60 requests per minute per user (JWT <c>sub</c>, else client IP).
/// Rejections use the shared 429 <c>rate_limited</c> response configured by <see cref="VocabularyRateLimits"/>.
/// </summary>
public static class DictionaryRateLimits
{
    public const string Lookup = "dictionary-lookup";

    public static IServiceCollection AddDictionaryRateLimits(this IServiceCollection services) =>
        services.Configure<RateLimiterOptions>(options =>
            options.AddPolicy(Lookup, context => RateLimitPartition.GetFixedWindowLimiter(
                context.User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value
                    ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 60, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
                })));
}
