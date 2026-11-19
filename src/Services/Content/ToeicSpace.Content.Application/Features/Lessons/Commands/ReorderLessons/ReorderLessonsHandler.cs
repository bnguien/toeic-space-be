using ToeicSpace.Content.Application.DTOs.Lessons;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.Lessons.Commands.ReorderLessons;

public sealed class ReorderLessonsHandler(LessonScopeService scope, ILessonRepository lessons,
    IUnitOfWork unitOfWork, TimeProvider clock) : IRequestHandler<ReorderLessonsCommand, IReadOnlyList<LessonSummaryDto>>
{
    public async Task<IReadOnlyList<LessonSummaryDto>> Handle(ReorderLessonsCommand request, CancellationToken ct)
    {
        await using var tx = await unitOfWork.BeginTransactionAsync(ct);
        var (course, module) = await scope.GetModuleAsync(request.CourseId, request.ModuleId, true, ct);
        var current = await lessons.ListAsync(module.Id, tracking: false, ct);
        if (request.LessonIds is null || request.LessonIds.Count != current.Count ||
            request.LessonIds.Distinct().Count() != request.LessonIds.Count ||
            !request.LessonIds.ToHashSet().SetEquals(current.Select(x => x.Id)))
            throw new ContentRuleException("invalid_lesson_order", "Provide each lesson ID of this module exactly once.");
        var now = clock.GetUtcNow().UtcDateTime;
        course.Touch(now);
        await unitOfWork.SaveChangesAsync(ct);
        var positions = request.LessonIds.Select((id, index) => (id, Position: index + 1))
            .ToDictionary(x => x.id, x => x.Position);
        await LessonOrdering.ReindexAsync(lessons, unitOfWork, module.Id, positions, current.Count + 1, now, ct);
        await tx.CommitAsync(ct);
        return (await lessons.ListAsync(module.Id, tracking: false, ct)).Select(x => x.ToSummary()).ToArray();
    }
}
