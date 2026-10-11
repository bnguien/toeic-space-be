namespace ToeicSpace.Identity.Application.Options;

public sealed class PasswordResetOptions
{
    public const string SectionName = "PasswordReset";

    public int OtpLifetimeMinutes { get; init; } = 5;
    public int ResetLifetimeMinutes { get; init; } = 5;
    public int ResendCooldownSeconds { get; init; } = 60;
    public int MaximumFailedAttempts { get; init; } = 5;
}
