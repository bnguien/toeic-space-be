using ToeicSpace.Identity.Application.Features.UserManagement.Results;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.RestoreUser;

/// <summary>
/// Undoes a soft delete. Role and status are kept as they were before the deletion.
/// </summary>
public sealed record RestoreUserCommand(
    Guid ActorId,
    Guid UserId) : IRequest<ManagedUserDetailResult>;
