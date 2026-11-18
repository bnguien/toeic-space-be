namespace ToeicSpace.Assessment.API.Security;

public static class AuthorizationPolicies
{
    /// <summary>
    /// Can create, edit and publish tests, passages, questions and practice sets.
    /// </summary>
    public const string ContentManager = "ContentManager";

    /// <summary>
    /// Role names issued by the Identity service (see UserRole).
    /// </summary>
    public static readonly string[] ContentManagerRoles = ["Admin", "Teacher"];

    /// <summary>
    /// Can read the analytics endpoints (aggregates over learner activity). The AI analyst calls them with a
    /// short-lived Admin token carrying scope agent.read, which is rejected on anything but GET/HEAD.
    /// </summary>
    public const string AnalyticsReader = "AnalyticsReader";

    public static readonly string[] AnalyticsReaderRoles = ["Admin"];
}
