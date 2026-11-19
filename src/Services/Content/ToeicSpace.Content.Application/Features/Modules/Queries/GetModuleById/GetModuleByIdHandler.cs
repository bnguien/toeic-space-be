using ToeicSpace.Content.Application.DTOs.Modules;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Modules.Queries.GetModuleById;

public sealed class GetModuleByIdHandler(ICourseRepository courses, ICourseModuleRepository modules,
    ILessonRepository lessons)
    : IRequestHandler<GetModuleByIdQuery, ModuleDto>
{
    public async Task<ModuleDto> Handle(GetModuleByIdQuery request, CancellationToken ct)
    {
        if (await courses.GetAsync(request.CourseId, ct) is null) throw ContentRuleException.NotFound();
        var module = await modules.GetAsync(request.CourseId, request.ModuleId, ct)
            ?? throw new ContentRuleException("module_not_found", "Module not found in this course.", 404);
        var items = await lessons.ListAsync(module.Id, tracking: false, ct);
        return module.ToDto() with { Lessons = items.Select(x => x.ToSummary()).ToArray() };
    }
}
