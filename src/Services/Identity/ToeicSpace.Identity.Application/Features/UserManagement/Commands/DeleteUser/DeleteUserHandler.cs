using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Interfaces.Persistence;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.DeleteUser;

public sealed class DeleteUserHandler : IRequestHandler<DeleteUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<DeleteUserHandler> _logger;

    public DeleteUserHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        TimeProvider timeProvider,
        ILogger<DeleteUserHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task Handle(
        DeleteUserCommand request,
        CancellationToken cancellationToken)
    {
        UserAdministrationRules.EnsureNotSelf(request.ActorId, request.UserId);

        await _userRepository.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var user = await UserAdministrationRules.GetExistingAsync(_userRepository, request.UserId, ct);
            UserAdministrationRules.EnsureNotDeleted(user);
            await UserAdministrationRules.EnsureAnotherAdministratorRemainsAsync(_userRepository, user, ct);

            user.DeletedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await _userRepository.SaveChangesAsync(ct);
            await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);

            _logger.LogInformation(
                "Administrator {ActorId} deleted user {UserId}",
                request.ActorId,
                user.Id);

            return user;
        }, cancellationToken);
    }
}
