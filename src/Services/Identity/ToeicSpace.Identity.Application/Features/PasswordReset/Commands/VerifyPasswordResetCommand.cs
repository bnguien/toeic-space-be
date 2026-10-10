using ToeicSpace.Identity.Application.Features.PasswordReset.Results;

namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed record VerifyPasswordResetCommand(string Email, string Otp)
    : IRequest<VerifyPasswordResetResult>
{
    public override string ToString() => nameof(VerifyPasswordResetCommand);
}
