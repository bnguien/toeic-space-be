using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.UnpublishLesson;

public sealed class UnpublishLessonHandler(LessonScopeService scope, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<UnpublishLessonCommand, LessonDto>
{
    public async Task<LessonDto> Handle(UnpublishLessonCommand request, CancellationToken ct)
    {
        var (course, _, lesson) = await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, true, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        lesson.Unpublish(now);
        course.Touch(now);
        await unitOfWork.SaveChangesAsync(ct);
        return lesson.ToDto();
    }
}
