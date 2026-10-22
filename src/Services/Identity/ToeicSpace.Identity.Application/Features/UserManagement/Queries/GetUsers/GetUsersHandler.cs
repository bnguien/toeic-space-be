using ToeicSpace.BuildingBlocks.Pagination;
using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Models;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUsers;

public sealed class GetUsersHandler
    : IRequestHandler<GetUsersQuery, PaginatedResult<ManagedUserResult>>
{
    private readonly IUserRepository _userRepository;

    public GetUsersHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<PaginatedResult<ManagedUserResult>> Handle(
        GetUsersQuery request,
        CancellationToken cancellationToken)
    {
        UserRole? role = UserAdministrationRules.TryParseRole(request.Role, out var parsedRole)
            ? parsedRole
            : null;
        UserStatus? status = UserAdministrationRules.TryParseStatus(request.Status, out var parsedStatus)
            ? parsedStatus
            : null;
        var sort = Enum.TryParse<UserSortOrder>(request.Sort?.Trim(), ignoreCase: true, out var parsedSort)
            ? parsedSort
            : UserSortOrder.Newest;

        var criteria = new UserSearchCriteria(
            string.IsNullOrWhiteSpace(request.Search) ? null : request.Search.Trim(),
            role,
            status,
            request.Deleted,
            sort,
            request.Page,
            request.PageSize);

        var (items, totalCount) = await _userRepository.SearchAsync(criteria, cancellationToken);

        return new PaginatedResult<ManagedUserResult>(
            items,
            totalCount,
            request.Page,
            request.PageSize);
    }
}
