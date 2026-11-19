using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticles;

/// <summary>
/// Cards of published articles + facets and the viewer's counts. <c>Length</c>: short | medium | long;
/// <c>State</c>: reading | done (signed-in viewer only). Unknown stage/topic values do not filter.
/// </summary>
public sealed record GetNewsArticlesQuery(string? Stage = null, string? Topic = null, string? Q = null,
    string? Length = null, string? State = null, int Page = 1, int PageSize = NewsCatalog.MaxPageSize,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<NewsArticleListDto>
{
    public const int MaxQueryLength = 80;
    public const int MaxPage = 500;
}
