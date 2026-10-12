using ToeicSpace.Identity.Application.Extensions;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;
using ToeicSpace.Identity.Application.Models;
using ToeicSpace.Identity.Domain.Entities;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Common.Sessions;

/// <summary>
/// Issues an access token and stores a new refresh token (hash only) for a user.
/// </summary>
public sealed class SessionIssuer
{
    private readonly IAccessTokenIssuer _accessTokenIssuer;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly SessionOptions _options;
    private readonly TimeProvider _timeProvider;

    public SessionIssuer(
        IAccessTokenIssuer accessTokenIssuer,
        IRefreshTokenGenerator refreshTokenGenerator,
        IRefreshTokenRepository refreshTokenRepository,
        SessionOptions options,
        TimeProvider timeProvider)
    {
        _accessTokenIssuer = accessTokenIssuer;
        _refreshTokenGenerator = refreshTokenGenerator;
        _refreshTokenRepository = refreshTokenRepository;
        _options = options;
        _timeProvider = timeProvider;
    }

    /// <param name="sessionExpiresAt">
    /// Expiry of the session being rotated; null starts a new session.
    /// </param>
    public async Task<AuthSession> IssueAsync(
        User user,
        DateTime? sessionExpiresAt,
        CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow();
        var accessToken = _accessTokenIssuer.Issue(user, now);
        var refreshToken = _refreshTokenGenerator.Generate();
        var refreshTokenExpiresAt = sessionExpiresAt
            ?? now.UtcDateTime.Add(_options.RefreshTokenLifetime);

        await _refreshTokenRepository.AddAsync(
            new UserToken
            {
                Id = Guid.NewGuid(),
                UserId = user.Id,
                TokenHash = _refreshTokenGenerator.Hash(refreshToken),
                Type = TokenType.RefreshToken,
                ExpiresAt = refreshTokenExpiresAt,
                IsRevoked = false,
                CreatedAt = now.UtcDateTime
            },
            cancellationToken);

        await _refreshTokenRepository.SaveChangesAsync(cancellationToken);

        await _refreshTokenRepository.EnforceSessionLimitAsync(
            user.Id,
            _options.MaxActiveSessions,
            now.UtcDateTime,
            cancellationToken);

        return new AuthSession(
            accessToken.Token,
            accessToken.ExpiresAt,
            refreshToken,
            new DateTimeOffset(DateTime.SpecifyKind(refreshTokenExpiresAt, DateTimeKind.Utc)),
            user.ToAuthenticatedUser());
    }
}
