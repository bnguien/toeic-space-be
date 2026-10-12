using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.VerifyEmail.Results;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.VerifyEmail.Commands;

public sealed class VerifyEmailHandler : IRequestHandler<VerifyEmailCommand, VerifyEmailResult>
{
    private const int MaximumFailedAttempts = 5;

    private readonly IUserRepository _userRepository;
    private readonly IOtpChallengeStore _otpChallengeStore;
    private readonly IOtpHasher _otpHasher;
    private readonly ILogger<VerifyEmailHandler> _logger;

    public VerifyEmailHandler(
        IUserRepository userRepository,
        IOtpChallengeStore otpChallengeStore,
        IOtpHasher otpHasher,
        ILogger<VerifyEmailHandler> logger)
    {
        _userRepository = userRepository;
        _otpChallengeStore = otpChallengeStore;
        _otpHasher = otpHasher;
        _logger = logger;
    }

    public async Task<VerifyEmailResult> Handle(
        VerifyEmailCommand request,
        CancellationToken cancellationToken)
    {
        var challenge = await _otpChallengeStore.ConsumeAsync(
            request.ChallengeId,
            _otpHasher.Hash(request.Otp),
            MaximumFailedAttempts,
            cancellationToken);

        if (challenge is null ||
            challenge.Purpose != OtpChallenge.VerifyEmailPurpose)
        {
            throw InvalidOtpException();
        }

        var user = await _userRepository.GetByIdAsync(
            challenge.UserId,
            cancellationToken);

        if (user is null)
        {
            throw AppException.NotFound(
                "User",
                challenge.UserId,
                ErrorCodes.UserNotFound);
        }

        var verifiedAt = DateTime.UtcNow;
        user.EmailVerifiedAt = verifiedAt;
        user.Status = UserStatus.Active;

        await _userRepository.SaveChangesAsync(cancellationToken);
        _logger.LogInformation(
            "Verified email for user {UserId}",
            user.Id);

        return new VerifyEmailResult(user.Id, verifiedAt);
    }

    private static AppException InvalidOtpException()
        => AppException.Validation(
            "The OTP is invalid or expired.",
            ErrorCodes.InvalidOrExpiredOtp);
}
