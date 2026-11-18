using ToeicSpace.Identity.Application.DTOs.Passwords;


namespace ToeicSpace.Identity.Application.Features.Passwords.Commands.VerifyPasswordReset;

public sealed record VerifyPasswordResetCommand(string Email, string Otp)
    : IRequest<VerifyPasswordResetResult>
{
    public override string ToString() => nameof(VerifyPasswordResetCommand);
}
