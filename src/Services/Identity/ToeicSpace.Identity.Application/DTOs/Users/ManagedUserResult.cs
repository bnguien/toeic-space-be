namespace ToeicSpace.Identity.Application.DTOs.Users;

/// <summary>
/// Row of the administrator user list. Role and Status are enum names,
/// "Inactive" meaning the email address has not been verified yet.
/// </summary>
public sealed record ManagedUserResult(
    Guid Id,
    string FullName,
    string Email,
    string? Phone,
    string? AvatarUrl,
    string Role,
    string Status,
    bool EmailVerified,
    DateTime CreatedAt,
    DateTime? DeletedAt,
    DateTime? LastActiveAt);
