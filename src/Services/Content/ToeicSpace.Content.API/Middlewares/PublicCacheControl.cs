namespace ToeicSpace.Content.API.Middlewares;

/// <summary>
/// Lets an endpoint opt out of the API-wide <c>Cache-Control: no-store</c> (security headers middleware) for public,
/// immutable content such as generated speech mp3s. The endpoint calls <see cref="Set"/>; this middleware sits before
/// the security headers middleware, so its OnStarting callback runs after theirs and has the last word.
/// </summary>
public static class PublicCacheControl
{
    private const string ItemKey = "public-cache-control";

    public static void Set(HttpContext context, int maxAgeSeconds) =>
        context.Items[ItemKey] = $"public, max-age={maxAgeSeconds}";

    public static IApplicationBuilder UsePublicCacheControl(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            context.Response.OnStarting(() =>
            {
                if (context.Response.StatusCode is StatusCodes.Status200OK or StatusCodes.Status206PartialContent
                    && context.Items.TryGetValue(ItemKey, out var value) && value is string cacheControl)
                {
                    context.Response.Headers.CacheControl = cacheControl;
                    context.Response.Headers.Remove("Pragma");
                }
                return Task.CompletedTask;
            });
            await next();
        });
}
