using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Features.PasswordReset.Results;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Options;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.PasswordReset.Commands;

public sealed class VerifyPasswordResetHandler(
    IPasswordResetStore resetStore,
    IUserRepository users,
    IOtpHasher otpHasher,
    IRefreshTokenGenerator tokenGenerator,
    PasswordResetOptions options,
    TimeProvider timeProvider) : IRequestHandler<VerifyPasswordResetCommand, VerifyPasswordResetResult>
{
    public async Task<VerifyPasswordResetResult> Handle(
        VerifyPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var token = tokenGenerator.Generate();
        var expiresAt = timeProvider.GetUtcNow().AddMinutes(options.ResetLifetimeMinutes);
        if (!await resetStore.VerifyAsync(
                IdentityNormalizer.NormalizeEmail(request.Email), otpHasher.Hash(request.Otp),
                tokenGenerator.Hash(token), cancellationToken))
        {
            throw AppException.Validation("The OTP is invalid or expired.", ErrorCodes.InvalidOrExpiredOtp);
        }

        var grant = await resetStore.GetGrantAsync(tokenGenerator.Hash(token), cancellationToken);
        var user = grant is null ? null : await users.GetByIdAsync(grant.UserId, cancellationToken);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash)
            || tokenGenerator.Hash(user.PasswordHash) != grant!.CredentialStamp)
        {
            await resetStore.ConsumeGrantAsync(tokenGenerator.Hash(token), cancellationToken);
            throw AppException.Validation("The OTP is invalid or expired.", ErrorCodes.InvalidOrExpiredOtp);
        }

        return new VerifyPasswordResetResult(token, expiresAt);
    }
}
