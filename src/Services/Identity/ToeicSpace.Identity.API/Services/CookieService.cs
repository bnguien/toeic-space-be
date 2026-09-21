using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.API.Services;

public sealed class CookieService : ICookieService
{
    private readonly IHttpContextAccessor _httpContextAccessor;

    public CookieService(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public void Set(
        string name,
        string value,
        DateTimeOffset expiresAt,
        string path)
    {
        HttpContext.Response.Cookies.Append(
            name,
            value,
            CreateOptions(path, expiresAt));
    }

    public string? Get(string name)
        => HttpContext.Request.Cookies[name];

    public void Delete(string name, string path)
        => HttpContext.Response.Cookies.Delete(
            name,
            CreateOptions(path, expiresAt: null));

    private static CookieOptions CreateOptions(string path, DateTimeOffset? expiresAt)
        => new()
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            Path = path,
            Expires = expiresAt,
            IsEssential = true
        };

    private HttpContext HttpContext
        => _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "Cookie operations require an active HTTP context.");
}
