using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Services;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.DeleteLesson;

public sealed class DeleteLessonHandler(LessonScopeService scope, ILessonRepository lessons,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<DeleteLessonCommand>
{
    public async Task Handle(DeleteLessonCommand request, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var (course, module, lesson) = await scope.GetLessonAsync(request.CourseId, request.ModuleId, request.LessonId, true, ct);
        var now = clock.GetUtcNow().UtcDateTime;
        course.Touch(now);
        await unitOfWork.SaveChangesAsync(ct);
        lessons.Remove(lesson);
        await unitOfWork.SaveChangesAsync(ct);
        var remaining = await lessons.ListAsync(module.Id, tracking: false, ct);
        var positions = remaining.Select((x, index) => (x.Id, Position: index + 1))
            .ToDictionary(x => x.Id, x => x.Position);
        await LessonOrdering.ReindexAsync(lessons, unitOfWork, module.Id, positions, remaining.Count + 1, now, ct);
        await tx.CommitAsync(ct);
    }
}
