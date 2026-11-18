namespace ToeicSpace.Identity.Application.Features.Passwords.Commands.RequestChangePasswordOtp;

public sealed record RequestChangePasswordOtpCommand(Guid UserId, string CurrentPassword) : IRequest
{
    public override string ToString() => nameof(RequestChangePasswordOtpCommand);
}
