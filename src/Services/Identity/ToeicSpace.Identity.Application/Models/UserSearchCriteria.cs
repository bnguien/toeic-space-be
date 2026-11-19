using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Models;

public sealed record UserSearchCriteria(
    string? Search,
    UserRole? Role,
    UserStatus? Status,
    bool Deleted,
    UserSortOrder Sort,
    int Page,
    int PageSize);
