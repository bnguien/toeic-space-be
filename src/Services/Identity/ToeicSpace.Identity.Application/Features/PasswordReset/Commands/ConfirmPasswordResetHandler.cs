using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed class ConfirmPasswordResetHandler(
    IPasswordResetStore resetStore,
    IUserRepository users,
    IPasswordRepository passwords,
    IPasswordHasher passwordHasher,
    IRefreshTokenGenerator tokenGenerator) : IRequestHandler<ConfirmPasswordResetCommand>
{
    public async Task Handle(ConfirmPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var tokenHash = tokenGenerator.Hash(request.Token);
        var grant = await resetStore.GetGrantAsync(tokenHash, cancellationToken);
        if (grant is null)
        {
            throw AppException.Validation(
                "The password reset session was not found or has expired.", ErrorCodes.TokenInvalid);
        }

        var user = await users.GetByIdAsync(grant.UserId, cancellationToken);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash))
        {
            throw AppException.Validation(
                "The account associated with the password reset session is no longer eligible for a password reset.",
                ErrorCodes.TokenInvalid);
        }

        if (tokenGenerator.Hash(user.PasswordHash) != grant.CredentialStamp)
        {
            throw AppException.Validation(
                "The account password has changed since the password reset session was issued.",
                ErrorCodes.TokenInvalid);
        }

        if (passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw AppException.Validation("The new password must differ from the current password.",
                ErrorCodes.PasswordSameAsOld);
        }

        var newHash = passwordHasher.Hash(request.NewPassword);
        if (!await resetStore.ConsumeGrantAsync(tokenHash, cancellationToken))
        {
            throw AppException.Validation(
                "The password reset session could not be consumed because it has expired, was already used, or was replaced.",
                ErrorCodes.TokenInvalid);
        }

        if (!await passwords.TryChangeAsync(user.Id, user.PasswordHash, newHash, cancellationToken))
        {
            throw AppException.Validation(
                "The password could not be reset because the account or its password changed during the request.",
                ErrorCodes.TokenInvalid);
        }
    }
}
