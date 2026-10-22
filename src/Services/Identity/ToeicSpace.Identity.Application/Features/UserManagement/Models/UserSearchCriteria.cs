using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Models;

public sealed record UserSearchCriteria(
    string? Search,
    UserRole? Role,
    UserStatus? Status,
    bool Deleted,
    UserSortOrder Sort,
    int Page,
    int PageSize);

public enum UserSortOrder
{
    Newest,
    Oldest,
    Name,
    LastActive
}

/// <summary>
/// Every count except Deleted excludes soft-deleted users.
/// </summary>
public sealed record UserCounts(
    int Total,
    int Active,
    int Suspended,
    int Unverified,
    int Deleted,
    int CreatedSince);

/// <summary>A sign-in or session refresh: each one creates a refresh token.</summary>
public sealed record SignIn(Guid UserId, DateTime At);
