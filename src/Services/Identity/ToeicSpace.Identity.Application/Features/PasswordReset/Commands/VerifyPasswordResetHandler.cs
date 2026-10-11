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
            throw AppException.Validation(
                "The password reset OTP is incorrect, has expired, was already used, or has exceeded the maximum failed attempts.",
                ErrorCodes.InvalidOrExpiredOtp);
        }

        var grant = await resetStore.GetGrantAsync(tokenGenerator.Hash(token), cancellationToken);
        if (grant is null)
        {
            await resetStore.ConsumeGrantAsync(tokenGenerator.Hash(token), cancellationToken);
            throw AppException.Validation(
                "The verified password reset session was not found or has expired.",
                ErrorCodes.InvalidOrExpiredOtp);
        }

        var user = await users.GetByIdAsync(grant.UserId, cancellationToken);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash))
        {
            await resetStore.ConsumeGrantAsync(tokenGenerator.Hash(token), cancellationToken);
            throw AppException.Validation(
                "The account associated with the verified OTP is no longer eligible for a password reset.",
                ErrorCodes.InvalidOrExpiredOtp);
        }

        if (tokenGenerator.Hash(user.PasswordHash) != grant.CredentialStamp)
        {
            await resetStore.ConsumeGrantAsync(tokenGenerator.Hash(token), cancellationToken);
            throw AppException.Validation(
                "The account password has changed since the password reset OTP was issued.",
                ErrorCodes.InvalidOrExpiredOtp);
        }

        return new VerifyPasswordResetResult(token, expiresAt);
    }
}
