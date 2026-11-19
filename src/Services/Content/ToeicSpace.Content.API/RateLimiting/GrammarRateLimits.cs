using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Content.API.RateLimiting;

/// <summary>
/// Per-user (JWT <c>sub</c>, else client IP) fixed windows of the grammar endpoints, as Studychill's throttles for a
/// signed-in user: submit 60/min, answer/retry 240/min, reveal 120/min, comment 20/min. Rejections share the 429
/// ProblemDetails (<c>rate_limited</c>) configured by <see cref="VocabularyRateLimits"/>.
/// </summary>
public static class GrammarRateLimits
{
    public const string Submit = "grammar-submit";
    public const string Answer = "grammar-answer";
    public const string Reveal = "grammar-reveal";
    public const string Comment = "grammar-comment";

    public static IServiceCollection AddGrammarRateLimits(this IServiceCollection services) =>
        services.Configure<RateLimiterOptions>(options =>
        {
            Add(options, Submit, 60);
            Add(options, Answer, 240);
            Add(options, Reveal, 120);
            Add(options, Comment, 20);
        });

    private static void Add(RateLimiterOptions options, string policy, int permitsPerMinute) =>
        options.AddPolicy(policy, context => RateLimitPartition.GetFixedWindowLimiter(
            context.User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value
                ?? context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = permitsPerMinute, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true
            }));
}
