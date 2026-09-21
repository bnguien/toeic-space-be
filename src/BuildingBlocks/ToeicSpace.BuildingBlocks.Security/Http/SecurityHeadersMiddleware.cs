using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;

namespace ToeicSpace.BuildingBlocks.Security.Http;

/// <summary>
/// Adds hardening headers to every API response.
/// </summary>
public sealed class SecurityHeadersMiddleware
{
    private const string ApiContentSecurityPolicy = "default-src 'none'; frame-ancestors 'none'";

    private readonly RequestDelegate _next;
    private readonly SecurityHeadersOptions _options;

    public SecurityHeadersMiddleware(RequestDelegate next, SecurityHeadersOptions options)
    {
        _next = next;
        _options = options;
    }

    public Task InvokeAsync(HttpContext context)
    {
        var isExcluded = _options.ExcludedPathPrefixes.Any(prefix =>
            context.Request.Path.StartsWithSegments(prefix, StringComparison.OrdinalIgnoreCase));

        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;

            headers[HeaderNames.XContentTypeOptions] = "nosniff";
            headers[HeaderNames.XFrameOptions] = "DENY";
            headers["Referrer-Policy"] = "no-referrer";
            headers["Cross-Origin-Resource-Policy"] = "same-origin";
            headers.Remove(HeaderNames.Server);
            headers.Remove("X-Powered-By");

            if (isExcluded)
            {
                return Task.CompletedTask;
            }

            if (_options.ApplyApiContentSecurityPolicy)
            {
                headers[HeaderNames.ContentSecurityPolicy] = ApiContentSecurityPolicy;
            }

            // Content, answer keys and tokens must not be stored by browsers or shared caches.
            if (_options.NoStore)
            {
                headers[HeaderNames.CacheControl] = "no-store";
                headers[HeaderNames.Pragma] = "no-cache";
            }

            return Task.CompletedTask;
        });

        return _next(context);
    }
}

public sealed class SecurityHeadersOptions
{
    public bool NoStore { get; init; } = true;

    public bool ApplyApiContentSecurityPolicy { get; init; } = true;

    /// <summary>
    /// Paths such as Swagger UI that serve HTML and must keep their own caching and scripts.
    /// </summary>
    public IReadOnlyList<string> ExcludedPathPrefixes { get; init; } = ["/swagger"];
}
