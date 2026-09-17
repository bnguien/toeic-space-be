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
        DateTimeOffset expiresAt)
    {
        HttpContext.Response.Cookies.Append(
            name,
            value,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/",
                Expires = expiresAt
            });
    }

    public string? Get(string name)
        => HttpContext.Request.Cookies[name];

    public void Delete(string name)
        => HttpContext.Response.Cookies.Delete(
            name,
            new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Path = "/"
            });

    private HttpContext HttpContext
        => _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException(
                "Cookie operations require an active HTTP context.");
}
