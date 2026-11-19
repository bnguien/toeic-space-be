using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;

namespace ToeicSpace.Content.Application.Services;

internal static class LessonOrdering
{
    internal static async Task ReindexAsync(ILessonRepository lessons, IUnitOfWork unitOfWork,
        Guid moduleId, IReadOnlyDictionary<Guid, int> positions, int offset, DateTime now, CancellationToken ct)
    {
        if (positions.Count == 0) return;
        await lessons.ShiftOrdersAsync(moduleId, offset, ct);
        var tracked = await lessons.ListAsync(moduleId, tracking: true, ct);
        foreach (var lesson in tracked)
        {
            lesson.SortOrder = positions[lesson.Id];
            lesson.UpdatedAt = now;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }
}
