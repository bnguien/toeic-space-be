using ToeicSpace.Identity.API.Middlewares;
using ToeicSpace.Identity.API.Services;
using ToeicSpace.Identity.Application.Interfaces.Security;
using System.Threading.RateLimiting;

namespace ToeicSpace.Identity.API;

public static class DependencyInjection
{
    public static IServiceCollection AddApiServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddExceptionHandler<GlobalExceptionHandler>();
        services.AddProblemDetails();

        services.AddEndpointsApiExplorer();
        services.AddHttpContextAccessor();
        services.AddScoped<ICookieService, CookieService>();
        services.AddControllers();
        services.AddSwaggerGen(options => options.EnableAnnotations());

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
                        .AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowCredentials();
                }
            }));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy("auth", httpContext =>
            {
                var ipAddress = httpContext.Connection.RemoteIpAddress?
                    .ToString() ?? "unknown";
                var partitionKey = $"{ipAddress}:{httpContext.Request.Path}";

                return RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey,
                    _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = 5,
                        Window = TimeSpan.FromMinutes(1),
                        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                        QueueLimit = 0,
                        AutoReplenishment = true
                    });
            });
        });

        return services;
    }

    public static WebApplication UseApiServices(
        this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseCors("Frontend");
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        return app;
    }
}
