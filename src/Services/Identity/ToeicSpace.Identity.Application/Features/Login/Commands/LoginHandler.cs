using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Common.Sessions;
using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Features.Register.Commands;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.Login.Commands;

public sealed class LoginHandler : IRequestHandler<LoginCommand, AuthSession>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILoginAttemptLimiter _loginAttemptLimiter;
    private readonly SessionIssuer _sessionIssuer;
    private readonly ILogger<LoginHandler> _logger;

    public LoginHandler(
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        ILoginAttemptLimiter loginAttemptLimiter,
        SessionIssuer sessionIssuer,
        ILogger<LoginHandler> logger)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _loginAttemptLimiter = loginAttemptLimiter;
        _sessionIssuer = sessionIssuer;
        _logger = logger;
    }

    public async Task<AuthSession> Handle(
        LoginCommand request,
        CancellationToken cancellationToken)
    {
        var email = IdentityNormalizer.NormalizeEmail(request.Email);

        // Checked before the password so a locked address cannot be used to test passwords.
        if (await _loginAttemptLimiter.IsLockedOutAsync(email, cancellationToken))
        {
            throw AppException.TooManyRequests(
                "Too many failed sign-in attempts. Please try again later.",
                ErrorCodes.LoginTemporarilyLocked);
        }

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        // Always runs the key derivation, even for unknown emails, to keep response times uniform.
        var passwordMatches = _passwordHasher.Verify(request.Password, user?.PasswordHash);

        if (user is null || user.DeletedAt is not null || !passwordMatches)
        {
            await _loginAttemptLimiter.RecordFailureAsync(email, cancellationToken);

            _logger.LogWarning(
                "Failed sign-in attempt for {UserId}",
                user?.Id.ToString() ?? "unknown account");

            throw InvalidCredentials();
        }

        // The password is proven at this point, so these messages do not leak account existence.
        if (user.EmailVerifiedAt is null)
        {
            throw AppException.Forbidden(
                "Please verify your email address before signing in.",
                ErrorCodes.EmailNotVerified);
        }

        if (!user.CanSignIn())
        {
            _logger.LogWarning("Sign-in refused for inactive user {UserId}", user.Id);

            throw AppException.Forbidden(
                "This account is not allowed to sign in.",
                ErrorCodes.AccountLocked);
        }

        await _loginAttemptLimiter.ResetAsync(email, cancellationToken);

        var session = await _sessionIssuer.IssueAsync(user, sessionExpiresAt: null, cancellationToken);

        _logger.LogInformation(
            "User {UserId} signed in with role {Role}",
            user.Id,
            user.Role);

        return session;
    }

    private static AppException InvalidCredentials()
        => AppException.Unauthenticated(
            "Email or password is incorrect.",
            ErrorCodes.InvalidCredentials);
}
