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
}
