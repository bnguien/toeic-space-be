using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessons;

public sealed class GetLessonsHandler(LessonScopeService scope, ILessonRepository lessons)
    : IRequestHandler<GetLessonsQuery, IReadOnlyList<LessonSummaryDto>>
{
    public async Task<IReadOnlyList<LessonSummaryDto>> Handle(GetLessonsQuery request, CancellationToken ct)
    {
        await scope.GetModuleAsync(request.CourseId, request.ModuleId, false, ct);
        return (await lessons.ListAsync(request.ModuleId, tracking: false, ct)).Select(x => x.ToSummary()).ToArray();
    }
}
