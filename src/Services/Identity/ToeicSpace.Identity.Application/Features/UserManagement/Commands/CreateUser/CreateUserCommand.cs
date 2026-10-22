using ToeicSpace.Identity.Application.Features.UserManagement.Results;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Commands.CreateUser;

/// <summary>
/// Creates an active, verified account with a generated temporary password.
/// The administrator vouches for the email address, so no verification OTP is sent.
/// </summary>
public sealed record CreateUserCommand(
    Guid ActorId,
    string FullName,
    string Email,
    string? Phone,
    string Role) : IRequest<CreateUserResult>;
