using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.OpenApi;
using ToeicSpace.Assessment.API.Middlewares;
using ToeicSpace.Assessment.API.Security;
using ToeicSpace.Assessment.API.Services;
using ToeicSpace.Assessment.Application.Interfaces.Security;
using ToeicSpace.BuildingBlocks.Security.Extensions;
using ToeicSpace.BuildingBlocks.Security.Jwt;

namespace ToeicSpace.Assessment.API;

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
        services.AddScoped<ICurrentUserService, CurrentUserService>();

        services.AddControllers()
            .AddJsonOptions(options =>
            {
                options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
                options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
            });

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
        app.UseRateLimiter();
        app.UseMiddleware<ContentAccessAuditMiddleware>();

        return app;
    }

    private static void AddAuthentication(
        IServiceCollection services,
        IConfiguration configuration)
    {
        // ES256 tokens from the Identity service, validated with its public key only.
        services.AddToeicSpaceJwtAuthentication(configuration);

        services.AddAuthorizationBuilder()
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())
            .AddPolicy(AuthorizationPolicies.ContentManager, policy => policy
                .RequireAuthenticatedUser()
                .RequireRole(AuthorizationPolicies.ContentManagerRoles));
    }

    /// <summary>
    /// Caps how fast one account or client can read the question bank, which limits bulk scraping
    /// of answer keys even with a stolen content-manager token.
    /// </summary>
    private static void AddRateLimiting(IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(httpContext =>
            {
                var userId = httpContext.User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value;

                return userId is not null
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        $"user:{userId}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 240,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        })
                    : RateLimitPartition.GetFixedWindowLimiter(
                        $"ip:{httpContext.Connection.RemoteIpAddress}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = 120,
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0
                        });
            });
        });
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
                Description = "Access token issued by the Identity service."
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

        // No origins configured means no cross-origin access (same behaviour as the Identity service).
        services.AddCors(options => options.AddPolicy(
            "Frontend",
            policy =>
            {
                if (allowedOrigins.Length > 0)
                {
                    policy.WithOrigins(allowedOrigins)
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            }));
    }
}
