using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.ChangePassword.Commands;

public sealed class ChangePasswordHandler(
    IUserRepository users,
    IPasswordRepository passwords,
    IPasswordHasher passwordHasher,
    IPasswordResetStore otpStore,
    IOtpHasher otpHasher,
    IRefreshTokenGenerator tokenGenerator) : IRequestHandler<ChangePasswordCommand>
{
    public async Task Handle(ChangePasswordCommand request, CancellationToken cancellationToken)
    {
        var user = await users.GetByIdAsync(request.UserId, cancellationToken);
        var matches = passwordHasher.Verify(request.CurrentPassword, user?.PasswordHash);
        if (user is null || !user.CanSignIn() || string.IsNullOrEmpty(user.PasswordHash))
        {
            throw AppException.Validation(
                "The account was not found or is no longer eligible for a password change.",
                ErrorCodes.InvalidCredentials);
        }

        if (!matches)
        {
            throw AppException.Validation("The current password is incorrect.", ErrorCodes.InvalidCredentials);
        }

        if (passwordHasher.Verify(request.NewPassword, user.PasswordHash))
        {
            throw AppException.Validation("The new password must differ from the current password.",
                ErrorCodes.PasswordSameAsOld);
        }

        // User-id keys keep change-password OTPs separate from recovery OTPs keyed by email.
        var tokenHash = tokenGenerator.Hash(tokenGenerator.Generate());
        if (!await otpStore.VerifyAsync(user.Id.ToString("N"), otpHasher.Hash(request.Otp),
                tokenHash, cancellationToken))
        {
            throw AppException.Validation("The password change OTP is incorrect, expired, or already used.",
                ErrorCodes.InvalidOrExpiredOtp);
        }

        var grant = await otpStore.GetGrantAsync(tokenHash, cancellationToken);
        if (grant is null || grant.UserId != user.Id
            || grant.CredentialStamp != tokenGenerator.Hash(user.PasswordHash))
        {
            await otpStore.ConsumeGrantAsync(tokenHash, cancellationToken);
            throw AppException.Validation("The password change OTP is no longer valid.",
                ErrorCodes.InvalidOrExpiredOtp);
        }

        var newHash = passwordHasher.Hash(request.NewPassword);
        if (!await otpStore.ConsumeGrantAsync(tokenHash, cancellationToken))
        {
            throw AppException.Validation("The password change OTP has expired or was already used.",
                ErrorCodes.InvalidOrExpiredOtp);
        }

        if (!await passwords.TryChangeAsync(user.Id, user.PasswordHash, newHash, cancellationToken))
        {
            throw AppException.Validation(
                "The password could not be changed because the account or its password changed during the request.",
                ErrorCodes.InvalidCredentials);
        }
    }
}
