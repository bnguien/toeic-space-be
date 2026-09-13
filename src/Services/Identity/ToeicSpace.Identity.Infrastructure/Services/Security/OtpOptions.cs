namespace ToeicSpace.Identity.Infrastructure.Services.Security;

public sealed class OtpOptions
{
    public const string SectionName = "Otp";

    public string HmacSecret { get; init; } = string.Empty;

    public int ChallengeTtlMinutes { get; init; } = 5;

    public int ResendCooldownSeconds { get; init; } = 60;
}
