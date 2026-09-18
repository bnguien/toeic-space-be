using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using ToeicSpace.BuildingBlocks.Security.Extensions;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.API.Middlewares;
using ToeicSpace.Identity.API.Security;
using ToeicSpace.Identity.API.Services;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.API;

public static class DependencyInjection
{
    private const string BearerSchemeName = "Bearer";

    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddKestrelHardening();
        services.AddTrustedForwardedHeaders(configuration);

        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        services.AddScoped<ICookieService, CookieService>();
        services.AddSingleton(
            configuration.GetSection(RefreshCookieOptions.SectionName).Get<RefreshCookieOptions>()
            ?? new RefreshCookieOptions());
        services.AddControllers();

        AddSwagger(services);
        AddAuthentication(services, configuration);
        AddCors(services, configuration);
        AddRateLimiting(services);

        return services;
    }

    public static WebApplication UseApiServices(
        this WebApplication app)
    {
        app.UseSecurityHeaders();
        app.UseExceptionHandler();
        app.UseCors("Frontend");
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        return app;
    }

    private static void AddAuthentication(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // Identity validates its own tokens with the public half of the signing key.
        using var signingKey = EcdsaKeyLoader.LoadPrivateKey(
            configuration[$"{AccessTokenDefaults.SectionName}:PrivateKey"],
            $"{AccessTokenDefaults.SectionName}:PrivateKey");

        services.AddToeicSpaceJwtAuthentication(configuration, signingKey);

        // Endpoints are private unless they opt out with [AllowAnonymous].
        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build());
    }

    private static void AddSwagger(IServiceCollection services)
    {
        services.AddSwaggerGen(options =>
        {
            options.EnableAnnotations();

            options.AddSecurityDefinition(BearerSchemeName, new OpenApiSecurityScheme
            {
                Type = SecuritySchemeType.Http,
                Scheme = "bearer",
                BearerFormat = "JWT",
                Description = "Access token returned by /api/auth/login."
            });

            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                [new OpenApiSecuritySchemeReference(BearerSchemeName, document)] = []
            });
        });
    }

    private static void AddCors(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>()
            ?? [];

        services.AddCors(options => options.AddPolicy(
            "Frontend",
            policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .WithHeaders("Content-Type", "Authorization", RequireCsrfHeaderAttribute.HeaderName)
                        .WithMethods("GET", "POST")
                        .AllowCredentials();
                }
            }));
    }

    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.AddPolicy(RateLimitPolicies.Credentials, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    $"{GetClientIp(httpContext)}:{httpContext.Request.Path}",
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));

            options.AddPolicy(RateLimitPolicies.Session, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    GetClientIp(httpContext),
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 30,
                        Window = TimeSpan.FromMinutes(1),
                        QueueLimit = 0,
                        AutoReplenishment = true
                    }));
        });
    }

    // Resolved by the forwarded-headers middleware when the request comes through the gateway.
    private static string GetClientIp(HttpContext httpContext)
        => httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
}
