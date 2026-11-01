using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.SetLessonFreePreview;

public sealed class SetLessonFreePreviewHandler(LessonScopeService scope, IUnitOfWork unitOfWork,
    TimeProvider clock) : IRequestHandler<SetLessonFreePreviewCommand, LessonDto>
{
    public async Task<LessonDto> Handle(SetLessonFreePreviewCommand request, CancellationToken ct)
    {
        var (course, _, lesson) = await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, true, ct);
        lesson.IsFreePreview = request.IsFreePreview;
        lesson.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        course.Touch(lesson.UpdatedAt);
        await unitOfWork.SaveChangesAsync(ct);
        return lesson.ToDto();
    }
}
