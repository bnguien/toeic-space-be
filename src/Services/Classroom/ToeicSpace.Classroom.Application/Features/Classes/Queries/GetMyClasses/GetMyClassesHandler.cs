using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyClasses;

public sealed class GetMyClassesHandler(IClassRepository db) : IRequestHandler<GetMyClassesQuery, PagedResult<ClassDto>>
{
    public async Task<PagedResult<ClassDto>> Handle(GetMyClassesQuery request, CancellationToken ct)
    {
        var teacherId = request.TeacherId;
        var page = request.Page;
        var pageSize = request.PageSize;


        var result = await db.GetTeacherClassesAsync(teacherId, page, pageSize, ct);
        return new PagedResult<ClassDto>(result.Items.Select(x => x.Summary()).ToArray(), result.Page, result.PageSize, result.TotalCount);
    }
}
