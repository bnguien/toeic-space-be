using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class NewsReadingRepository(ContentDbContext db) : INewsReadingRepository
{
    public Task<NewsReading?> GetForUpdateAsync(Guid learnerId, Guid articleId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.NewsReadings
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.ArticleId == articleId, ct));

    public Task<NewsReading?> GetAsync(Guid learnerId, Guid articleId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => db.NewsReadings.AsNoTracking()
            .FirstOrDefaultAsync(x => x.LearnerId == learnerId && x.ArticleId == articleId, ct));

    public Task<IReadOnlyList<NewsReading>> GetForArticlesForUpdateAsync(Guid learnerId,
        IReadOnlyCollection<Guid> articleIds, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<NewsReading>>(async () =>
            await db.NewsReadings.Where(x => x.LearnerId == learnerId && articleIds.Contains(x.ArticleId))
                .ToListAsync(ct));

    public Task<IReadOnlyDictionary<Guid, NewsCardProgress>> GetProgressAsync(Guid learnerId,
        IReadOnlyCollection<Guid> articleIds, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyDictionary<Guid, NewsCardProgress>>(async () =>
            await db.NewsReadings.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && articleIds.Contains(x.ArticleId))
                .Select(x => new { x.ArticleId, x.CompletedAt, x.ReadingSeconds })
                .ToDictionaryAsync(x => x.ArticleId, x => new NewsCardProgress(x.CompletedAt != null, x.ReadingSeconds),
                    ct));

    public Task<(int Done, int Reading)> CountAsync(Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<(int, int)>(async () =>
        {
            var rows = db.NewsReadings.AsNoTracking().Where(x => x.LearnerId == learnerId
                && x.Article!.Status == NewsArticleStatus.Published);
            var done = await rows.CountAsync(x => x.CompletedAt != null, ct);
            var reading = await rows.CountAsync(x => x.CompletedAt == null, ct);
            return (done, reading);
        });

    public Task<(NewsReaderSettings? Settings, DateTime? UpdatedAt)> GetLatestSettingsAsync(Guid learnerId,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<(NewsReaderSettings?, DateTime?)>(async () =>
        {
            var row = await db.NewsReadings.AsNoTracking()
                .Where(x => x.LearnerId == learnerId && x.SettingsUpdatedAt != null)
                .OrderByDescending(x => x.SettingsUpdatedAt)
                .Select(x => new { x.Settings, x.SettingsUpdatedAt })
                .FirstOrDefaultAsync(ct);
            return (row?.Settings, row?.SettingsUpdatedAt);
        });

    public void Add(NewsReading reading) => db.NewsReadings.Add(reading);
}
