using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;

public sealed class GetClassesHandler(IClassRepository db) : IRequestHandler<GetClassesQuery, PagedResult<ClassDto>>
{
    public async Task<PagedResult<ClassDto>> Handle(GetClassesQuery request, CancellationToken ct)
    {
        var search = new ClassSearchCriteria(request.Search, request.Status, request.TeacherId, request.StartDateFrom,
            request.StartDateTo, request.Page, request.PageSize);


        var result = await db.SearchAsync(search, ct);
        return new PagedResult<ClassDto>(result.Items.Select(x => x.Summary()).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }
}
