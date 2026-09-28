namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class LoginLockoutOptions
{
    public const string SectionName = "LoginLockout";

    public int MaxFailedAttempts { get; init; } = 5;

    /// <summary>
    /// How long failures are counted and how long the lockout lasts.
    /// </summary>
    public TimeSpan LockoutDuration { get; init; } = TimeSpan.FromMinutes(15);
}
