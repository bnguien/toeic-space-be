using ToeicSpace.Identity.Application.DTOs.Users;


namespace ToeicSpace.Identity.Application.Features.Users.Commands.ChangeUserStatus;

/// <summary>
/// Suspends (Suspended) or reactivates (Active) a verified account.
/// Suspending ends every session of the user.
/// </summary>
public sealed record ChangeUserStatusCommand(
    Guid ActorId,
    Guid UserId,
    string Status) : IRequest<ManagedUserDetailResult>;
