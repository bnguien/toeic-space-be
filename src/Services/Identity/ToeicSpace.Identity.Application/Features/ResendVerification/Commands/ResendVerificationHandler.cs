using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.ResendVerification.Results;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Messaging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.ResendVerification.Commands;

public sealed class ResendVerificationHandler
    : IRequestHandler<ResendVerificationCommand, ResendVerificationResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IOtpHasher _otpHasher;
    private readonly IOtpChallengeStore _otpChallengeStore;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ILogger<ResendVerificationHandler> _logger;

    public ResendVerificationHandler(
        IUserRepository userRepository,
        IOtpGenerator otpGenerator,
        IOtpHasher otpHasher,
        IOtpChallengeStore otpChallengeStore,
        IIntegrationEventPublisher eventPublisher,
        ILogger<ResendVerificationHandler> logger)
    {
        _userRepository = userRepository;
        _otpGenerator = otpGenerator;
        _otpHasher = otpHasher;
        _otpChallengeStore = otpChallengeStore;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<ResendVerificationResult> Handle(
        ResendVerificationCommand request,
        CancellationToken cancellationToken)
    {
        var email = request.Email.Trim().ToLowerInvariant();
        var user = await _userRepository.GetByEmailAsync(
            email,
            cancellationToken);

        if (user is null || user.EmailVerifiedAt is not null)
        {
            return CreateOpaqueResult();
        }

        var otp = _otpGenerator.Generate();
        var challengeId = Guid.NewGuid().ToString("N");
        var challenge = new OtpChallenge(
            user.Id,
            _otpHasher.Hash(otp),
            OtpChallenge.VerifyEmailPurpose,
            0);

        await _otpChallengeStore.StoreAsync(
            challengeId,
            challenge,
            cancellationToken);

        await _eventPublisher.PublishAsync(
            new UserRegistrationOtpRequestedEvent(
                user.Email,
                user.FullName,
                otp),
            cancellationToken);

        _logger.LogInformation(
            "Requested another email verification OTP for user {UserId}",
            user.Id);

        return new ResendVerificationResult(user.Id, challengeId);
    }

    private static ResendVerificationResult CreateOpaqueResult()
        => new(Guid.NewGuid(), Guid.NewGuid().ToString("N"));
}
