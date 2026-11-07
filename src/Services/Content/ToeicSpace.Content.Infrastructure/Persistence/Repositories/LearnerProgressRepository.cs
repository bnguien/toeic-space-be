using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class LearnerProgressRepository(ContentDbContext db) : ILearnerProgressRepository
{
    public Task<LearnerWordProgress?> GetForUpdateAsync(Guid learnerId, Guid wordId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.LearnerWordProgress
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.WordId == wordId, ct));

    public Task<IReadOnlyList<LearnerWordProgress>> GetForWordsAsync(Guid learnerId,
        IReadOnlyCollection<Guid> wordIds, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<LearnerWordProgress>>(async () =>
            await db.LearnerWordProgress.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && wordIds.Contains(x.WordId)).ToListAsync(ct));

    public Task<IReadOnlyList<LearnerWordProgress>> GetAllAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<LearnerWordProgress>>(async () =>
            await db.LearnerWordProgress.AsNoTracking().Where(x => x.LearnerId == learnerId).ToListAsync(ct));

    public Task<IReadOnlyList<LearnerWordProgress>> GetDueAsync(Guid learnerId, DateOnly localToday, int limit,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<LearnerWordProgress>>(async () =>
            await db.LearnerWordProgress.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && x.DueOn <= localToday)
                .OrderBy(x => x.DueOn).ThenBy(x => x.LastReviewedAt).ThenBy(x => x.Id)
                .Take(limit).ToListAsync(ct));

    public Task<PagedResult<LearnerWordProgress>> GetMistakesAsync(Guid learnerId, int page, int pageSize,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            var query = Mistakes(learnerId);
            var count = await query.CountAsync(ct);
            var items = await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
            return new PagedResult<LearnerWordProgress>(items, page, pageSize, count);
        });

    public Task<IReadOnlyList<Guid>> GetMistakeWordIdsAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<Guid>>(async () =>
            await Mistakes(learnerId).Select(x => x.WordId).ToListAsync(ct));

    public Task<int> CountMistakesAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.LearnerWordProgress
            .CountAsync(x => x.LearnerId == learnerId && x.MistakePending, ct));

    private IQueryable<LearnerWordProgress> Mistakes(Guid learnerId) => db.LearnerWordProgress.AsNoTracking()
        .Where(x => x.LearnerId == learnerId && x.MistakePending)
        .OrderByDescending(x => x.LastMistakeAt).ThenBy(x => x.Id);

    public Task<WordReviewLog?> GetReviewLogAsync(Guid learnerId, Guid clientEventId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.WordReviewLogs.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.ClientEventId == clientEventId, ct));

    public Task<IReadOnlyList<DateOnly>> GetReviewDaysAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<DateOnly>>(async () =>
            await db.WordReviewLogs.AsNoTracking().Where(x => x.LearnerId == learnerId)
                .Select(x => x.LocalDay).Distinct().OrderByDescending(x => x).ToListAsync(ct));

    public Task<int> CountReviewsOnAsync(Guid learnerId, DateOnly localDay, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.WordReviewLogs
            .CountAsync(x => x.LearnerId == learnerId && x.LocalDay == localDay, ct));

    public void Add(LearnerWordProgress progress) => db.LearnerWordProgress.Add(progress);
    public void AddReviewLog(WordReviewLog log) => db.WordReviewLogs.Add(log);

    public Task RemoveAllForWordAsync(Guid wordId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(async () =>
        {
            db.LearnerWordProgress.RemoveRange(await db.LearnerWordProgress.Where(x => x.WordId == wordId).ToListAsync(ct));
            db.WordReviewLogs.RemoveRange(await db.WordReviewLogs.Where(x => x.WordId == wordId).ToListAsync(ct));
            return true;
        });
}
