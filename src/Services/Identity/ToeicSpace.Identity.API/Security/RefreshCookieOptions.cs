namespace ToeicSpace.Identity.API.Security;

public sealed class RefreshCookieOptions
{
    public const string SectionName = "RefreshCookie";

    /// <summary>
    /// The __Secure- prefix makes browsers reject the cookie unless it is Secure and set over HTTPS
    /// (or localhost).
    /// </summary>
    public string Name { get; init; } = "__Secure-ts_rt";

    /// <summary>
    /// Path the browser sees, including the gateway prefix, so the cookie is only sent to the auth endpoints.
    /// </summary>
    public string Path { get; init; } = "/identity/api/auth";
}
