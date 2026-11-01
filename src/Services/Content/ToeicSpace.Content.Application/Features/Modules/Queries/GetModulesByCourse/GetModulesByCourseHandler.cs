using ToeicSpace.Content.Application.DTOs.Modules;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Modules.Queries.GetModulesByCourse;

public sealed class GetModulesByCourseHandler(ICourseRepository courses, ICourseModuleRepository modules)
    : IRequestHandler<GetModulesByCourseQuery, IReadOnlyList<ModuleDto>>
{
    public async Task<IReadOnlyList<ModuleDto>> Handle(GetModulesByCourseQuery request, CancellationToken ct)
    {
        if (await courses.GetAsync(request.CourseId, ct) is null) throw ContentRuleException.NotFound();
        return (await modules.ListAsync(request.CourseId, tracking: false, ct)).Select(x => x.ToDto()).ToArray();
    }
}
