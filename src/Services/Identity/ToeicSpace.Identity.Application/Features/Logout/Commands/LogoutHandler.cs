using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.Application.Features.Logout.Commands;

/// <summary>
/// Revokes the current refresh token. Always succeeds so a stale cookie can still sign out.
/// </summary>
public sealed class LogoutHandler : IRequestHandler<LogoutCommand>
{
    private const int MaxRefreshTokenLength = 128;

    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly IRefreshTokenGenerator _refreshTokenGenerator;
    private readonly ILogger<LogoutHandler> _logger;

    public LogoutHandler(
        IRefreshTokenRepository refreshTokenRepository,
        IRefreshTokenGenerator refreshTokenGenerator,
        ILogger<LogoutHandler> logger)
    {
        _refreshTokenRepository = refreshTokenRepository;
        _refreshTokenGenerator = refreshTokenGenerator;
        _logger = logger;
    }

    public async Task Handle(
        LogoutCommand request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken)
            || request.RefreshToken.Length > MaxRefreshTokenLength)
        {
            return;
        }

        var token = await _refreshTokenRepository.GetByHashAsync(
            _refreshTokenGenerator.Hash(request.RefreshToken),
            cancellationToken);

        if (token is null || token.IsRevoked)
        {
            return;
        }

        await _refreshTokenRepository.RevokeAsync(token.Id, cancellationToken);

        _logger.LogInformation("User {UserId} signed out", token.UserId);
    }
}
