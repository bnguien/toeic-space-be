using System.Threading.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Extensions;

const string CsrfHeaderName = "X-CSRF-Protection";

string[] credentialEndpoints =
[
    "/identity/api/auth/login",
    "/identity/api/auth/register",
    "/identity/api/auth/verify-email",
    "/identity/api/auth/resend-verification"
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

app.UseSecurityHeaders();

app.UseCors();

app.UseRateLimiter();

app.MapReverseProxy();

app.Run();
