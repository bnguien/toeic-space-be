using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface INewsReadingRepository
{
    Task<NewsReading?> GetForUpdateAsync(Guid learnerId, Guid articleId, CancellationToken ct);
    /// <summary>Untracked current row (used after a concurrent first open).</summary>
    Task<NewsReading?> GetAsync(Guid learnerId, Guid articleId, CancellationToken ct);
    Task<IReadOnlyList<NewsReading>> GetForArticlesForUpdateAsync(Guid learnerId,
        IReadOnlyCollection<Guid> articleIds, CancellationToken ct);
    /// <summary>Done flag and reading clock of the learner for these articles.</summary>
    Task<IReadOnlyDictionary<Guid, NewsCardProgress>> GetProgressAsync(Guid learnerId,
        IReadOnlyCollection<Guid> articleIds, CancellationToken ct);
    /// <summary>Completed / opened-not-completed readings of published articles.</summary>
    Task<(int Done, int Reading)> CountAsync(Guid learnerId, CancellationToken ct);
    /// <summary>The learner's most recently written reader settings (from any reading) and when they were written.</summary>
    Task<(NewsReaderSettings? Settings, DateTime? UpdatedAt)> GetLatestSettingsAsync(Guid learnerId,
        CancellationToken ct);
    void Add(NewsReading reading);
}
