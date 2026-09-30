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
        if (string.IsNullOrEmpty(request.Token) || request.Token.Length > 128)
        {
            throw InvalidToken();
        }

        var tokenHash = tokenGenerator.Hash(request.Token);
        var grant = await resetStore.GetGrantAsync(tokenHash, cancellationToken);
        if (grant is null)
        {
            throw InvalidToken();
        }

        var user = await users.GetByIdAsync(grant.UserId, cancellationToken);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash)
            || tokenGenerator.Hash(user.PasswordHash) != grant.CredentialStamp)
        {
            throw InvalidToken();
        }

        if (passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw AppException.Validation("The new password must differ from the current password.",
                ErrorCodes.PasswordSameAsOld);
        }

        var newHash = passwordHasher.Hash(request.NewPassword);
        if (!await resetStore.ConsumeGrantAsync(tokenHash, cancellationToken)
            || !await passwords.TryChangeAsync(user.Id, user.PasswordHash, newHash, cancellationToken))
        {
            throw InvalidToken();
        }
    }

    private static AppException InvalidToken()
        => AppException.Validation("The password reset session is invalid or expired. Request a new OTP.",
            ErrorCodes.TokenInvalid);
}
