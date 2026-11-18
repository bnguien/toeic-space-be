namespace ToeicSpace.Identity.API.Security;

public static class AuthorizationPolicies
{
    /// <summary>
    /// Can read the analytics endpoints (aggregates over accounts and sign-ins). The AI analyst calls them with a
    /// short-lived Admin token carrying scope agent.read, which is rejected on anything but GET/HEAD.
    /// </summary>
    public const string AnalyticsReader = "AnalyticsReader";

    /// <summary>Role names issued by the Identity service (see UserRole).</summary>
    public static readonly string[] AnalyticsReaderRoles = ["Admin"];
}
