namespace ToeicSpace.Identity.Application.DTOs.Users;

/// <summary>
/// The temporary password is only returned here; it is never stored in plain text.
/// </summary>
public sealed record CreateUserResult(
    ManagedUserDetailResult User,
    string TemporaryPassword);
