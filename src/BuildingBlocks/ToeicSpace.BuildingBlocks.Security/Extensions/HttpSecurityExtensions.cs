using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ToeicSpace.BuildingBlocks.Security.Http;

namespace ToeicSpace.BuildingBlocks.Security.Extensions;

public static class HttpSecurityExtensions
{
    public const long DefaultMaxRequestBodyBytes = 2 * 1024 * 1024;

    /// <summary>
    /// Trusts X-Forwarded-For/Proto only from the API gateway, so rate limits and logs see the real client IP.
    /// Loopback is trusted by default; add container networks under ForwardedHeaders:KnownNetworks.
    /// </summary>
    public static IServiceCollection AddTrustedForwardedHeaders(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var knownNetworks = configuration
            .GetSection("ForwardedHeaders:KnownNetworks")
            .Get<string[]>()
            ?? [];

        services.Configure<ForwardedHeadersOptions>(options =>
        {
            options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            options.ForwardLimit = 1;

            foreach (var network in knownNetworks)
            {
                options.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
            }
        });

        return services;
    }

    /// <summary>
    /// Hides the Kestrel server banner and caps request bodies.
    /// </summary>
    public static IServiceCollection AddKestrelHardening(
        this IServiceCollection services,
        long maxRequestBodyBytes = DefaultMaxRequestBodyBytes)
    {
        services.Configure<KestrelServerOptions>(options =>
        {
            options.AddServerHeader = false;
            options.Limits.MaxRequestBodySize = maxRequestBodyBytes;
        });

        return services;
    }

    public static IApplicationBuilder UseSecurityHeaders(
        this IApplicationBuilder app,
        SecurityHeadersOptions? options = null)
        => app.UseMiddleware<SecurityHeadersMiddleware>(options ?? new SecurityHeadersOptions());
}
