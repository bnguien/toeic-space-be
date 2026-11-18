namespace ToeicSpace.Identity.Application.Features.Passwords.Commands.ConfirmPasswordReset;

public sealed record ConfirmPasswordResetCommand(string Token, string NewPassword, string ConfirmPassword)
    : IRequest
{
    public override string ToString() => nameof(ConfirmPasswordResetCommand);
}
