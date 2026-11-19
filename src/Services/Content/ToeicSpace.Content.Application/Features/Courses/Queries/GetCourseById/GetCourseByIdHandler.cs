using ToeicSpace.Content.Application.DTOs.Courses;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetCourseById;

public sealed class GetCourseByIdHandler(ICourseRepository courses, ICourseModuleRepository modules)
    : IRequestHandler<GetCourseByIdQuery, CourseDto>
{
    public async Task<CourseDto> Handle(GetCourseByIdQuery request, CancellationToken ct)
    {
        var course = await courses.GetAsync(request.CourseId, ct) ?? throw ContentRuleException.NotFound();
        var items = await modules.ListAsync(course.Id, tracking: false, ct);
        return course.ToDto() with { Modules = items.Select(x => x.ToSummary()).ToArray() };
    }
}
