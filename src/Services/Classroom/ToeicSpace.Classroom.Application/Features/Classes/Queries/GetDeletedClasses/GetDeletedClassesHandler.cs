using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetDeletedClasses;

public sealed class GetDeletedClassesHandler(IClassRepository classes)
    : IRequestHandler<GetDeletedClassesQuery, PagedResult<ClassDto>>
{
    public async Task<PagedResult<ClassDto>> Handle(GetDeletedClassesQuery request, CancellationToken ct)
    {
        var result = await classes.GetDeletedAsync(request.Search, request.Page, request.PageSize, ct);
        return new PagedResult<ClassDto>(result.Items.Select(x => x.Summary()).ToArray(),
            result.Page, result.PageSize, result.TotalCount);
    }
}
