using ToeicSpace.BuildingBlocks.Pagination;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUsers;

/// <summary>
/// Role, Status and Sort are enum names. Deleted switches between active accounts and the trash.
/// </summary>
public sealed record GetUsersQuery(
    string? Search,
    string? Role,
    string? Status,
    bool Deleted,
    string? Sort,
    int Page = 1,
    int PageSize = 20) : IRequest<PaginatedResult<ManagedUserResult>>;
