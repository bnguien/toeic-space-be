namespace ToeicSpace.Identity.Application.Features.ChangePassword.Commands;

public sealed record RequestChangePasswordOtpCommand(Guid UserId, string CurrentPassword) : IRequest
{
    public override string ToString() => nameof(RequestChangePasswordOtpCommand);
}
