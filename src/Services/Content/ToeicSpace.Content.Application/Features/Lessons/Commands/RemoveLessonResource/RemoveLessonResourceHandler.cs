using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.RemoveLessonResource;

public sealed class RemoveLessonResourceHandler(LessonScopeService scope, ILessonRepository lessons,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<RemoveLessonResourceCommand>
{
    public async Task Handle(RemoveLessonResourceCommand request, CancellationToken ct)
    {
        var (course, _, lesson) = await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, true, ct);
        var resource = lesson.Resources.SingleOrDefault(x => x.Id == request.ResourceId)
            ?? throw new ContentRuleException("resource_not_found", "Resource not found in this lesson.", 404);
        if (lesson.Status == LessonStatus.Published && lesson.Resources.Count == 1 &&
            string.IsNullOrWhiteSpace(lesson.TextContent) && lesson.Videos.Count == 0)
            throw new ContentRuleException("publish_requirements", "Published lesson must retain content.", 409);
        course.Touch(clock.GetUtcNow().UtcDateTime);
        lessons.RemoveResource(resource);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
