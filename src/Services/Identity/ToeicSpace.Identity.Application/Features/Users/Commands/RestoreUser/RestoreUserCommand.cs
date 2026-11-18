using ToeicSpace.Identity.Application.DTOs.Users;


namespace ToeicSpace.Identity.Application.Features.Users.Commands.RestoreUser;

/// <summary>
/// Undoes a soft delete. Role and status are kept as they were before the deletion.
/// </summary>
public sealed record RestoreUserCommand(
    Guid ActorId,
    Guid UserId) : IRequest<ManagedUserDetailResult>;
