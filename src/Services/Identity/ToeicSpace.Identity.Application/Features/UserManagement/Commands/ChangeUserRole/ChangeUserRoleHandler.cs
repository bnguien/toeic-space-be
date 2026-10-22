using Microsoft.Extensions.Logging;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserRole;

public sealed class ChangeUserRoleHandler
    : IRequestHandler<ChangeUserRoleCommand, ManagedUserDetailResult>
{
    private readonly IUserRepository _userRepository;
    private readonly IRefreshTokenRepository _refreshTokenRepository;
    private readonly ManagedUserReader _reader;
    private readonly ILogger<ChangeUserRoleHandler> _logger;

    public ChangeUserRoleHandler(
        IUserRepository userRepository,
        IRefreshTokenRepository refreshTokenRepository,
        ManagedUserReader reader,
        ILogger<ChangeUserRoleHandler> logger)
    {
        _userRepository = userRepository;
        _refreshTokenRepository = refreshTokenRepository;
        _reader = reader;
        _logger = logger;
    }

    public async Task<ManagedUserDetailResult> Handle(
        ChangeUserRoleCommand request,
        CancellationToken cancellationToken)
    {
        UserAdministrationRules.EnsureNotSelf(request.ActorId, request.UserId);
        UserAdministrationRules.TryParseRole(request.Role, out var role);

        var updated = await _userRepository.ExecuteInSerializableTransactionAsync(async ct =>
        {
            var user = await UserAdministrationRules.GetExistingAsync(_userRepository, request.UserId, ct);
            UserAdministrationRules.EnsureNotDeleted(user);

            if (user.Role == role)
            {
                return user;
            }

            if (role != UserRole.Admin)
            {
                await UserAdministrationRules.EnsureAnotherAdministratorRemainsAsync(_userRepository, user, ct);
            }

            var previousRole = user.Role;
            user.Role = role;
            await _userRepository.SaveChangesAsync(ct);
            await _refreshTokenRepository.RevokeAllForUserAsync(user.Id, ct);

            _logger.LogInformation(
                "Administrator {ActorId} changed the role of user {UserId} from {PreviousRole} to {Role}",
                request.ActorId,
                user.Id,
                previousRole,
                role);

            return user;
        }, cancellationToken);

        return await _reader.ReadAsync(updated, cancellationToken);
    }
}
