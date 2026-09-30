namespace ToeicSpace.Identity.Application.Features.ChangePassword.Commands;

public sealed record ChangePasswordCommand(
    Guid UserId, string CurrentPassword, string NewPassword, string ConfirmPassword) : IRequest
{
    public override string ToString() => nameof(ChangePasswordCommand);
}
