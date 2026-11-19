using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetCourses;

public sealed class GetCoursesHandler(ICourseRepository courses)
    : IRequestHandler<GetCoursesQuery, PagedResult<CourseDto>>
{
    public async Task<PagedResult<CourseDto>> Handle(GetCoursesQuery request, CancellationToken ct)
    {
        var page = await courses.SearchAsync(request.Search, request.Status, request.Level, request.ExamType,
            request.Page, request.PageSize, ct);
        return new PagedResult<CourseDto>(page.Items.Select(x => x.ToDto()).ToArray(),
            page.Page, page.PageSize, page.TotalCount);
    }
}
