using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Mappings;

namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClasses;

public sealed class GetMyEnrolledClassesHandler(IClassRepository classes)
    : IRequestHandler<GetMyEnrolledClassesQuery, PagedResult<ClassDto>>
{
    public async Task<PagedResult<ClassDto>> Handle(GetMyEnrolledClassesQuery request, CancellationToken ct)
    {
        var page = await classes.GetStudentClassesAsync(request.StudentId, request.Search, request.Status,
            request.Page, request.PageSize, ct);
        return new PagedResult<ClassDto>(page.Items.Select(x => x.Class.Summary() with { TeacherName = x.TeacherName }).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}
