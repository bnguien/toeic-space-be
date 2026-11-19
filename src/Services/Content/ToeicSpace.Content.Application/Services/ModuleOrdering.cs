using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;

namespace ToeicSpace.Content.Application.Services;

internal static class ModuleOrdering
{
    // Shifting into a disjoint range prevents transient unique-index collisions while renumbering.
    internal static async Task ReindexAsync(ICourseModuleRepository modules, IUnitOfWork unitOfWork,
        Guid courseId, IReadOnlyDictionary<Guid, int> positions, int offset, DateTime now, CancellationToken ct)
    {
        if (positions.Count == 0) return;
        await modules.ShiftOrdersAsync(courseId, offset, ct);
        var tracked = await modules.ListAsync(courseId, tracking: true, ct);
        foreach (var module in tracked)
        {
            module.SortOrder = positions[module.Id];
            module.UpdatedAt = now;
        }
        await unitOfWork.SaveChangesAsync(ct);
    }
}
