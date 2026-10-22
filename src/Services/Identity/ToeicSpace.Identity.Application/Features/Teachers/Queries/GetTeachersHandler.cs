using ToeicSpace.Identity.Application.Features.Teachers.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;

namespace ToeicSpace.Identity.Application.Features.Teachers.Queries;

public sealed class GetTeachersHandler(IUserRepository users)
    : IRequestHandler<GetTeachersQuery, GetTeachersResult>
{
    public async Task<GetTeachersResult> Handle(GetTeachersQuery request, CancellationToken cancellationToken)
    {
        var (items, totalCount) = await users.SearchActiveTeachersAsync(
            request.Search,
            request.Page,
            request.PageSize,
            cancellationToken);

        return new GetTeachersResult(
            items.Select(x => new TeacherResult(x.Id, x.FullName, x.Email, x.AvatarUrl, x.Status.ToString())).ToArray(),
            request.Page,
            request.PageSize,
            totalCount);
    }
}
