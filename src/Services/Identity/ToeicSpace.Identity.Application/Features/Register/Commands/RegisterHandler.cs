using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.Register.Results;
using ToeicSpace.Identity.Application.Interfaces.Caching;
using ToeicSpace.Identity.Application.Interfaces.Messaging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Messaging.Events;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.Register.Commands;

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, RegisterResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IOtpGenerator _otpGenerator;
    private readonly IOtpHasher _otpHasher;
    private readonly IOtpChallengeStore _otpChallengeStore;
    private readonly IIntegrationEventPublisher _eventPublisher;
    private readonly ILogger<RegisterHandler> _logger;

    public RegisterHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IOtpGenerator otpGenerator,
        IOtpHasher otpHasher,
        IOtpChallengeStore otpChallengeStore,
        IIntegrationEventPublisher eventPublisher,
        ILogger<RegisterHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _otpGenerator = otpGenerator;
        _otpHasher = otpHasher;
        _otpChallengeStore = otpChallengeStore;
        _eventPublisher = eventPublisher;
        _logger = logger;
    }

    public async Task<RegisterResult> Handle(
        RegisterCommand request,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(
                request.Password,
                request.ConfirmPassword,
                StringComparison.Ordinal))
        {
            throw AppException.Validation(
                "Password and confirmation password do not match.",
                ErrorCodes.PasswordsDoNotMatch);
        }

        var email = IdentityNormalizer.NormalizeEmail(request.Email);
        var phone = IdentityNormalizer.NormalizePhone(request.Phone);

        if (!IdentityNormalizer.IsValidPhone(phone))
        {
            throw AppException.Validation(
                "Phone must be a valid international phone number.",
                ErrorCodes.ValidationError);
        }

        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw RegistrationCouldNotBeCompleted();
        }

        if (phone is not null &&
            await _userRepository.PhoneExistsAsync(phone, cancellationToken))
        {
            throw RegistrationCouldNotBeCompleted();
        }

        var now = DateTime.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Email = email,
            Phone = phone,
            PasswordHash = _passwordHasher.Hash(request.Password),
            EmailVerifiedAt = null,
            Role = UserRole.User,
            Status = UserStatus.Inactive,
            CreatedAt = now
        };

        try
        {
            await _userRepository.AddAsync(user, cancellationToken);
            await _userRepository.SaveChangesAsync(cancellationToken);
        }
        catch (DuplicateUserException)
        {
            throw RegistrationCouldNotBeCompleted();
        }

        var otp = _otpGenerator.Generate();
        var challenge = new OtpChallenge(
            user.Id,
            _otpHasher.Hash(otp),
            OtpChallenge.VerifyEmailPurpose,
            0);

        var challengeId = await _otpChallengeStore.ReplaceChallengeAsync(
            challenge,
            cancellationToken);

        await _eventPublisher.PublishAsync(
            new UserRegistrationOtpRequestedEvent(
                user.Email,
                user.FullName,
                otp),
            cancellationToken);

        _logger.LogInformation(
            "Registered user {UserId} and requested an email verification OTP",
            user.Id);

        return new RegisterResult(user.Id, challengeId);
    }

    private static AppException RegistrationCouldNotBeCompleted()
        => AppException.Conflict(
            "Unable to complete registration with the provided information.",
            ErrorCodes.RegistrationCouldNotBeCompleted);
}

internal static partial class IdentityNormalizer
{
    public static string NormalizeEmail(string email)
        => email.Trim().ToLowerInvariant();

    public static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone))
        {
            return null;
        }

        var normalized = PhoneSeparatorRegex().Replace(phone.Trim(), string.Empty);

        return normalized.StartsWith("00", StringComparison.Ordinal)
            ? $"+{normalized[2..]}"
            : normalized.StartsWith("0", StringComparison.Ordinal)
                ? $"+84{normalized[1..]}"
                : normalized;
    }

    public static bool IsValidPhone(string? phone)
    {
        var normalized = NormalizePhone(phone);

        return normalized is null || NormalizedPhoneRegex().IsMatch(normalized);
    }

    [GeneratedRegex(@"[\s().-]+")]
    private static partial Regex PhoneSeparatorRegex();

    [GeneratedRegex(@"^\+?[1-9]\d{7,14}$")]
    private static partial Regex NormalizedPhoneRegex();
}
