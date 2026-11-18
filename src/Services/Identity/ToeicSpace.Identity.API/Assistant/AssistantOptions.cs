namespace ToeicSpace.Identity.API.Assistant;

/// <summary>
/// How the AI assistant (SeasonedAgents) reads TOEIC Space: it trades this key for a short read-only token.
/// Only the SHA-256 of the key is configured here (user-secrets / environment); the key lives in SeasonedAgents' vault.
/// The token request may name the admin the assistant works for (<see cref="AgentOnBehalf"/>): that value only
/// lands in the token's <c>act</c> claim for audit and never widens what the token may do.
/// </summary>
public sealed class AgentAccessOptions
{
    public const string SectionName = "AgentAccess";

    public string? KeySha256 { get; init; }

    /// <summary>Subject written into assistant tokens (not a real account).</summary>
    public Guid SubjectId { get; init; }

    public TimeSpan TokenLifetime { get; init; } = TimeSpan.FromMinutes(10);
}

/// <summary>
/// Where the "Ask AI" panel of the admin area sends questions: SeasonedAgents' backend and its integration key.
/// </summary>
public sealed class SeasonedAgentsOptions
{
    public const string SectionName = "SeasonedAgents";

    public string? BaseUrl { get; init; }

    public string? ClientKey { get; init; }
}
