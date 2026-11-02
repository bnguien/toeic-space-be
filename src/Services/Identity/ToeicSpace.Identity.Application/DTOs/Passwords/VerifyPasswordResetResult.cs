namespace ToeicSpace.Identity.Application.DTOs.Passwords;

// Internal result only; the controller puts the credential in an HttpOnly cookie.
public sealed record VerifyPasswordResetResult(string Token, DateTimeOffset ExpiresAt)
{
    public override string ToString() => nameof(VerifyPasswordResetResult);
}
