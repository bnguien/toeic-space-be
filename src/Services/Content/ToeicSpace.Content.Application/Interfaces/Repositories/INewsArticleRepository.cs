using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface INewsArticleRepository
{
    /// <summary>
    /// One page of published articles and the total matching the filter. <c>State</c> (reading | done) lists the
    /// learner's readings (most recent first); a guest (<paramref name="learnerId"/> empty) gets nothing for it.
    /// </summary>
    Task<(IReadOnlyList<NewsArticleSummary> Items, int Total)> GetPublishedPageAsync(NewsArticleFilter filter,
        Guid learnerId, CancellationToken ct);

    /// <summary>Published article count per stage and per topic.</summary>
    Task<(IReadOnlyDictionary<string, int> Stage, IReadOnlyDictionary<string, int> Topic)> GetFacetsAsync(
        CancellationToken ct);

    Task<NewsArticle?> GetPublishedBySlugAsync(string slug, CancellationToken ct);
    Task<IReadOnlyList<NewsArticle>> GetPublishedBySlugsAsync(IReadOnlyCollection<string> slugs, CancellationToken ct);

    /// <summary>Tracked articles (any status) with these slugs, for the import.</summary>
    Task<IReadOnlyList<NewsArticle>> GetBySlugsForUpdateAsync(IReadOnlyCollection<string> slugs, CancellationToken ct);
    void Add(NewsArticle article);
}
