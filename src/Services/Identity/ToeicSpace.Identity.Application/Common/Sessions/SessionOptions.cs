namespace ToeicSpace.Identity.Application.Common.Sessions;

public sealed class SessionOptions
{
    public const string SectionName = "Session";

    /// <summary>
    /// Absolute lifetime of a sign-in. Rotating the refresh token does not extend it.
    /// </summary>
    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(7);

    /// <summary>
    /// A session that has not been refreshed for this long must sign in again.
    /// </summary>
    public TimeSpan IdleTimeout { get; init; } = TimeSpan.FromHours(24);

    public int MaxActiveSessions { get; init; } = 5;
}
