using ToeicSpace.Identity.Application.Features.UserManagement.Results;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.ChangeUserRole;

/// <summary>
/// Changes the role and ends every session of the user, because the role is part of the access token.
/// </summary>
public sealed record ChangeUserRoleCommand(
    Guid ActorId,
    Guid UserId,
    string Role) : IRequest<ManagedUserDetailResult>;
