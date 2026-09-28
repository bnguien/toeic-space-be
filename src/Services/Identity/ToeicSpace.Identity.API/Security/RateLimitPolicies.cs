namespace ToeicSpace.Identity.API.Security;

public static class RateLimitPolicies
{
    /// <summary>
    /// Endpoints that accept credentials or OTPs: 5 requests per minute per client IP and endpoint.
    /// </summary>
    public const string Credentials = "auth";

    /// <summary>
    /// Session maintenance (refresh, sign-out, profile): 30 requests per minute per client IP.
    /// </summary>
    public const string Session = "session";
}
