using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Content.API.RateLimiting;

/// <summary>
/// Per-user fixed-window limits (JWT <c>sub</c>, else client IP) for the enrichment endpoints: lookup 60/min,
/// import parse 30/min, import 20/min, AI fill 20/min. A rejected request gets 429 ProblemDetails, code
/// <c>rate_limited</c>, with Retry-After.
/// </summary>
public static class VocabularyRateLimits
{
    public const string Lookup = "vocabulary-lookup";
    public const string ImportParse = "vocabulary-import-parse";
    public const string Import = "vocabulary-import";
    public const string AiFill = "vocabulary-ai-fill";

    public static IServiceCollection AddVocabularyRateLimits(this IServiceCollection services) =>
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, ct) =>
            {
                if (context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var retryAfter))
                    context.HttpContext.Response.Headers.RetryAfter = ((int)retryAfter.TotalSeconds).ToString();
                var problem = new ProblemDetails
                {
                    Status = StatusCodes.Status429TooManyRequests, Title = "Too Many Requests",
                    Detail = "Too many requests. Please wait a moment and try again.",
                    Instance = context.HttpContext.Request.Path
                };
                problem.Extensions["code"] = "rate_limited";
                context.HttpContext.Response.ContentType = "application/problem+json";
                await context.HttpContext.Response.WriteAsJsonAsync(problem, ct);
            };
            Add(options, Lookup, 60);
            Add(options, ImportParse, 30);
            Add(options, Import, 20);
            Add(options, AiFill, 20);
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
