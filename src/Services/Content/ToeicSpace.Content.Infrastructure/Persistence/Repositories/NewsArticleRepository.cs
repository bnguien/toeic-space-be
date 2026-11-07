using Microsoft.EntityFrameworkCore;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Infrastructure.Persistence.Repositories;

public sealed class NewsArticleRepository(ContentDbContext db) : INewsArticleRepository
{
    private IQueryable<NewsArticle> Published =>
        db.NewsArticles.AsNoTracking().Where(x => x.Status == NewsArticleStatus.Published);

    public Task<(IReadOnlyList<NewsArticleSummary> Items, int Total)> GetPublishedPageAsync(NewsArticleFilter filter,
        Guid learnerId, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<(IReadOnlyList<NewsArticleSummary>, int)>(async () =>
        {
            var query = Filter(Published, filter);
            var skip = (filter.Page - 1) * filter.PageSize;
            if (filter.State is "reading" or "done")
            {
                if (learnerId == Guid.Empty) return ([], 0);
                var reading = filter.State == "reading";
                var joined = query.Join(
                    db.NewsReadings.AsNoTracking().Where(r => r.LearnerId == learnerId
                        && (reading ? r.CompletedAt == null : r.CompletedAt != null)),
                    a => a.Id, r => r.ArticleId, (a, r) => new { Article = a, Reading = r });
                var total = await joined.CountAsync(ct);
                var ordered = reading
                    ? joined.OrderByDescending(x => x.Reading.OpenedAt)
                    : joined.OrderByDescending(x => x.Reading.CompletedAt);
                var rows = await ordered.Skip(skip).Take(filter.PageSize).Select(x => x.Article)
                    .Select(Summary).ToListAsync(ct);
                return (rows, total);
            }
            var count = await query.CountAsync(ct);
            var items = await query
                .OrderByDescending(x => x.Featured).ThenByDescending(x => x.PublishedAt).ThenByDescending(x => x.Slug)
                .Skip(skip).Take(filter.PageSize).Select(Summary).ToListAsync(ct);
            return (items, count);
        });

    public Task<(IReadOnlyDictionary<string, int> Stage, IReadOnlyDictionary<string, int> Topic)> GetFacetsAsync(
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<(IReadOnlyDictionary<string, int>, IReadOnlyDictionary<string, int>)>(
            async () =>
            {
                var stages = await Published.GroupBy(x => x.Stage).Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
                var topics = await Published.GroupBy(x => x.Topic).Select(g => new { g.Key, Count = g.Count() })
                    .ToDictionaryAsync(x => x.Key, x => x.Count, ct);
                return (stages, topics);
            });

    public Task<NewsArticle?> GetPublishedBySlugAsync(string slug, CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync(() => Published.FirstOrDefaultAsync(x => x.Slug == slug, ct));

    public Task<IReadOnlyList<NewsArticle>> GetPublishedBySlugsAsync(IReadOnlyCollection<string> slugs,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<NewsArticle>>(async () =>
            await Published.Where(x => slugs.Contains(x.Slug)).ToListAsync(ct));

    public Task<IReadOnlyList<NewsArticle>> GetBySlugsForUpdateAsync(IReadOnlyCollection<string> slugs,
        CancellationToken ct) =>
        PersistenceExceptionTranslator.ExecuteAsync<IReadOnlyList<NewsArticle>>(async () =>
            await db.NewsArticles.Where(x => slugs.Contains(x.Slug)).ToListAsync(ct));

    public void Add(NewsArticle article) => db.NewsArticles.Add(article);

    private static IQueryable<NewsArticle> Filter(IQueryable<NewsArticle> query, NewsArticleFilter filter)
    {
        if (NewsCatalog.IsStage(filter.Stage)) query = query.Where(x => x.Stage == filter.Stage);
        if (NewsCatalog.IsTopic(filter.Topic)) query = query.Where(x => x.Topic == filter.Topic);
        query = filter.Length switch
        {
            "short" => query.Where(x => x.Minutes < 3),
            "medium" => query.Where(x => x.Minutes >= 3 && x.Minutes <= 6),
            "long" => query.Where(x => x.Minutes > 6),
            _ => query
        };
        if (!string.IsNullOrWhiteSpace(filter.Query))
        {
            var text = filter.Query.Trim();
            query = query.Where(x => x.TitleEn.Contains(text) || (x.TitleVi != null && x.TitleVi.Contains(text))
                || x.SourceName.Contains(text));
        }
        return query;
    }

    private static readonly System.Linq.Expressions.Expression<Func<NewsArticle, NewsArticleSummary>> Summary =
        x => new NewsArticleSummary(x.Id, x.Slug, x.TitleEn, x.TitleVi, x.SummaryVi, x.SourceName, x.PublishedAt,
            x.Stage, x.Topic, x.Minutes, x.WordCount, x.CoverFile, x.CoverAlt, x.Featured);
}
