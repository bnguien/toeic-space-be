using ToeicSpace.Identity.Application.Features.UserManagement.Results;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.UpdateUser;

/// <summary>
/// Replaces the name and phone number. A null phone clears it.
/// The email is the sign-in identifier and cannot be changed here.
/// </summary>
public sealed record UpdateUserCommand(
    Guid ActorId,
    Guid UserId,
    string FullName,
    string? Phone) : IRequest<ManagedUserDetailResult>;
