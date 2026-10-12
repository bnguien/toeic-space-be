using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Common.Sessions;
using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Enums;
using ToeicSpace.Identity.Domain.Exceptions;

namespace ToeicSpace.Identity.Application.Features.RefreshSession.Commands;

/// <summary>
/// Rotates the refresh token. Each token works once; presenting a used token is treated
/// as theft and ends every session of the user.
/// </summary>
public sealed class RefreshSessionHandler : IRequestHandler<RefreshSessionCommand, AuthSession>
{
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IUserRepository _userRepository;
    private readonly SessionIssuer _sessionIssuer;
    private readonly SessionOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RefreshSessionHandler> _logger;

    public RefreshSessionHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenGenerator refreshTokenGenerator,
        IUserRepository userRepository,
        SessionIssuer sessionIssuer,
        SessionOptions options,
        TimeProvider timeProvider,
        ILogger<RefreshSessionHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenGenerator = refreshTokenGenerator;
        _userRepository = userRepository;
        _sessionIssuer = sessionIssuer;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<AuthSession> Handle(
        RefreshSessionCommand request,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        var tokenHash = _refreshTokenGenerator.Hash(request.RefreshToken);
        var token = await _refreshTokenRepository.GetByHashAsync(tokenHash, cancellationToken);

        if (token is null || token.Type != TokenType.RefreshToken)
        {
            throw SessionEnded(ErrorCodes.TokenInvalid);
        }

        if (token.IsRevoked)
        {
            throw SessionEnded(ErrorCodes.TokenRevoked);
        }

        if (token.UsedAt is not null)
        {
            await RevokeAllSessionsAsync(token.UserId, cancellationToken);
            throw SessionEnded(ErrorCodes.TokenRevoked);
        }

        if (token.ExpiresAt <= now || token.CreatedAt.Add(_options.IdleTimeout) <= now)
        {
            await _refreshTokenRepository.RevokeAsync(token.Id, cancellationToken);
            throw SessionEnded(ErrorCodes.TokenExpired);
        }

        // Two requests with the same token: only one may win the rotation.
        if (!await _refreshTokenRepository.TryMarkUsedAsync(token.Id, now, cancellationToken))
        {
            await RevokeAllSessionsAsync(token.UserId, cancellationToken);
            throw SessionEnded(ErrorCodes.TokenRevoked);
        }

        var user = await _userRepository.GetByIdAsync(token.UserId, cancellationToken);

        // Role or status changes take effect at the next refresh at the latest.
        if (user is null || !user.CanSignIn())
        {
            await _refreshTokenRepository.RevokeAllForUserAsync(token.UserId, cancellationToken);
            throw SessionEnded(ErrorCodes.TokenRevoked);
        }

        return await _sessionIssuer.IssueAsync(user, token.ExpiresAt, cancellationToken);
    }

    private async Task RevokeAllSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        await _refreshTokenRepository.RevokeAllForUserAsync(userId, cancellationToken);

        _logger.LogWarning(
            "Refresh token reuse detected for user {UserId}. All sessions were revoked",
            userId);
    }

    private static AppException SessionEnded(string code)
        => AppException.Unauthenticated(
            "Your session has ended. Please sign in again.",
            code);
}
