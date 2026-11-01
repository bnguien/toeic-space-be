using ToeicSpace.Identity.Application.Features.Students.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;

namespace ToeicSpace.Identity.Application.Features.Students.Queries;

public sealed class GetStudentsHandler(IUserRepository users)
    : IRequestHandler<GetStudentsQuery, GetStudentsResult>
{
    public async Task<GetStudentsResult> Handle(GetStudentsQuery request, CancellationToken ct)
    {
        var (items, totalCount) = await users.SearchActiveStudentsAsync(request.Search, request.Page, request.PageSize, ct);
        return new GetStudentsResult(items.Select(x =>
            new StudentResult(x.Id, x.FullName, x.Email, x.AvatarUrl, x.Status.ToString())).ToArray(),
            request.Page, request.PageSize, totalCount);
    }
}
