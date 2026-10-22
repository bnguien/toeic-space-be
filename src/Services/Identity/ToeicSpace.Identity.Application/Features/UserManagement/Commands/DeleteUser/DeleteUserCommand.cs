namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.DeleteUser;

/// <summary>
/// Soft delete: the account can no longer sign in and keeps its email until it is restored.
/// </summary>
public sealed record DeleteUserCommand(
    Guid ActorId,
    Guid UserId) : IRequest;
