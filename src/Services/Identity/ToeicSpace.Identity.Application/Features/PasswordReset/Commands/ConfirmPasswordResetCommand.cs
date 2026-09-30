namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed record ConfirmPasswordResetCommand(string Token, string NewPassword, string ConfirmPassword)
    : IRequest
{
    public override string ToString() => nameof(ConfirmPasswordResetCommand);
}
