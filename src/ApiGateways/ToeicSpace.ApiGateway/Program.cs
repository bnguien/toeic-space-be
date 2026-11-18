using ToeicSpace.BuildingBlocks.Security.Http;
using System.Threading.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Extensions;

const string CsrfHeaderName = "X-CSRF-Protection";

string[] credentialEndpoints =
[
    "/identity/api/auth/login",
    "/identity/api/auth/register",
    "/identity/api/auth/verify-email",
    "/identity/api/auth/resend-verification",
    "/identity/api/v1/auth/password-reset/otp",
    "/identity/api/v1/auth/password-reset/verify",
    "/identity/api/v1/auth/password-reset/confirm",
    "/identity/api/v1/auth/change-password",
    "/identity/api/v1/auth/change-password/otp"
];

var builder = WebApplication.CreateBuilder(args);

var allowedOrigins = builder.Configuration
    .GetSection("Cors:AllowedOrigins")
    .Get<string[]>()
    ?? [];

builder.Services.AddKestrelHardening();
builder.Services.AddTrustedForwardedHeaders(builder.Configuration);

// Only listed origins may call the API with credentials. The browser app is normally
// served from the same origin (reverse proxy), which needs no CORS at all.
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        if (allowedOrigins.Length == 0)
        {
            return;
        }

        policy.WithOrigins(allowedOrigins)
            .WithHeaders("Content-Type", "Authorization", CsrfHeaderName)
            .WithMethods("GET", "POST", "PUT", "PATCH", "DELETE")
            .AllowCredentials()
            .SetPreflightMaxAge(TimeSpan.FromMinutes(10));
    });
});

// Edge limits per client IP. Services apply their own, finer limits behind this.
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.GlobalLimiter = PartitionedRateLimiter.CreateChained(
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            RateLimitPartition.GetFixedWindowLimiter(
                $"all:{httpContext.Connection.RemoteIpAddress}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 300,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                })),
        PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            credentialEndpoints.Any(endpoint =>
                httpContext.Request.Path.Equals(endpoint, StringComparison.OrdinalIgnoreCase))
                ? RateLimitPartition.GetFixedWindowLimiter(
                    $"credentials:{httpContext.Connection.RemoteIpAddress}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 20,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0
                    })
                : RateLimitPartition.GetNoLimiter("unlimited")));
});

builder.Services
    .AddReverseProxy()
    .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

// News covers (Content service) are public, content-addressed images: keep their long cache headers.
app.UseSecurityHeaders(new SecurityHeadersOptions
{
    ExcludedPathPrefixes = ["/swagger", "/content/api/v1/news/covers"]
});

app.UseCors();

app.UseRateLimiter();

// Service-to-service endpoints are never reachable through the public reverse proxy.
app.Use(async (context, next) =>
{
    if (context.Request.Path.StartsWithSegments("/content/api/v1/internal", StringComparison.OrdinalIgnoreCase) ||
        context.Request.Path.StartsWithSegments("/payment/api/v1/internal", StringComparison.OrdinalIgnoreCase))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }
    await next();
});

app.MapReverseProxy();

app.Run();
