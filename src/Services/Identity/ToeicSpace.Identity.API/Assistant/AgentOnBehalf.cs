using System.Text.RegularExpressions;

namespace ToeicSpace.Identity.API.Assistant;

/// <summary>
/// Optional "on behalf of" headers of <c>POST api/v1/agent/token</c>. SeasonedAgents sends the admin it is working
/// for; the value is copied into the token's <c>act</c> claim for audit only and never decides what the token may
/// do (the token is always role Admin, scope agent.read, GET/HEAD only).
/// </summary>
public static partial class AgentOnBehalf
{
    public const string OfHeader = "X-On-Behalf-Of";

    public const string RoleHeader = "X-On-Behalf-Role";

    /// <summary>The only role an assistant may act for.</summary>
    public const string AdminRole = "admin";

    public const int MaxLength = 64;

    [GeneratedRegex("^[A-Za-z0-9_:-]{1,64}$")]
    private static partial Regex SafeActor();

    /// <summary>
    /// Validates the two headers. Returns false with an error message (for a 400) when a value is malformed;
    /// otherwise <paramref name="actor"/> is the value for the <c>act</c> claim, or null when no header was sent.
    /// </summary>
    public static bool TryParse(string? onBehalfOf, string? onBehalfRole, out string? actor, out string? error)
    {
        actor = null;
        error = null;

        if (onBehalfOf is null && onBehalfRole is null)
        {
            return true;
        }

        if (onBehalfOf is null || !SafeActor().IsMatch(onBehalfOf))
        {
            error = $"{OfHeader} must be 1-{MaxLength} characters of A-Z, a-z, 0-9, '_', ':' or '-'.";
            return false;
        }

        if (onBehalfRole is not null && !string.Equals(onBehalfRole.Trim(), AdminRole, StringComparison.OrdinalIgnoreCase))
        {
            error = $"{RoleHeader} must be '{AdminRole}'.";
            return false;
        }

        actor = onBehalfOf;
        return true;
    }
}
