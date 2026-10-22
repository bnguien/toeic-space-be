using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Interfaces.Persistence;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.RevokeUserSessions;

public sealed class RevokeUserSessionsHandler : IRequestHandler<RevokeUserSessionsCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ILogger<RevokeUserSessionsHandler> _logger;

    public RevokeUserSessionsHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ILogger<RevokeUserSessionsHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _logger = logger;
    }

    public async Task Handle(
        RevokeUserSessionsCommand request,
        CancellationToken cancellationToken)
    {
        // Signing out yourself goes through the normal logout, which also clears the cookie.
        UserAdministrationRules.EnsureNotSelf(request.ActorId, request.UserId);

        var user = await UserAdministrationRules.GetExistingAsync(
            _userRepository,
            request.UserId,
            cancellationToken);

        await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, cancellationToken);

        _logger.LogInformation(
            "Administrator {ActorId} ended every session of user {UserId}",
            request.ActorId,
            user.Id);
    }
}
