using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.ChangePassword.Commands;

public sealed class ChangePasswordHandler(
    IUserRepository users,
    IPasswordRepository passwords,
    IPasswordHasher passwordHasher) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        var matches = passwordHasher.Verify(request.CurrentPassword, user?.PasswordHash);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash) || !matches)
        {
            throw AppException.Validation("The current password is invalid.", ErrorCodes.InvalidCredentials);
        }

        if (passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw AppException.Validation("The new password must differ from the current password.",
                ErrorCodes.PasswordSameAsOld);
        }

        if (!await passwords.TryChangeAsync(user.Id, user.PasswordHash,
                passwordHasher.Hash(request.NewPassword), cancellationToken))
        {
            throw AppException.Validation("The current password is invalid.", ErrorCodes.InvalidCredentials);
        }
    }
}
