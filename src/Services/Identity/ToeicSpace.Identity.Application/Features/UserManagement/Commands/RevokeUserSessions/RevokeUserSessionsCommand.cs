namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.RevokeUserSessions;

/// <summary>
/// Signs the user out on every device. Access tokens already issued stay valid until they expire.
/// </summary>
public sealed record RevokeUserSessionsCommand(
    Guid ActorId,
    Guid UserId) : IRequest;
