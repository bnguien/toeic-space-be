using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.Lessons.Queries.GetLessonDetail;

public sealed class GetLessonDetailHandler(LessonScopeService scope) : IRequestHandler<GetLessonDetailQuery, LessonDto>
{
    public async Task<LessonDto> Handle(GetLessonDetailQuery request, CancellationToken ct) =>
        (await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, false, ct)).Lesson.ToDto();
}
