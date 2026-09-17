namespace ToeicSpace.Identity.Application.Models;

public sealed record OtpChallenge(
    Guid UserId,
    string OtpHash,
    string Purpose,
    int FailedAttempts)
{
    public const string VerifyEmailPurpose = "VERIFY_EMAIL";
}
