using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class DictationProgressRepository(ContentDbContext db) : IDictationProgressRepository
{
    public Task<IReadOnlyList<DictationProgress>> GetProgressAsync(Guid learnerId, IReadOnlyCollection<int>? itemIds,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<DictationProgress>>(async () =>
        {
            var query = db.DictationProgress.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && x.Item!.IsActive);
            if (itemIds is not null) query = query.Where(x => itemIds.Contains(x.ItemId));
            return await query.ToListAsync(ct);
        });

    public Task<IReadOnlyDictionary<int, int>> CountPassedByCourseAsync(Guid learnerId,
        IReadOnlyCollection<int> courseIds, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyDictionary<int, int>>(async () =>
            await db.DictationProgress.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && x.Passed && x.Item!.IsActive && courseIds.Contains(x.Item.CourseId))
                .GroupBy(x => x.Item!.CourseId)
                .Select(g => new { CourseId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.CourseId, x => x.Count, ct));

    public Task<DictationProgress?> GetForUpdateAsync(Guid learnerId, int itemId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            db.DictationProgress.FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.ItemId == itemId, ct));

    public void Add(DictationProgress progress) => db.DictationProgress.Add(progress);

    public Task<DictationLearnerState?> GetStateAsync(Guid learnerId, bool forUpdate, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() =>
            (forUpdate ? db.DictationLearnerStates : db.DictationLearnerStates.AsNoTracking())
                .FirstOrDefaultAsync(x => x.LearnerId == learnerId, ct));

    public void AddState(DictationLearnerState state) => db.DictationLearnerStates.Add(state);
}
