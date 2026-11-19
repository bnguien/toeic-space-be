using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.News;

namespace ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticles;

public sealed class GetNewsArticlesHandler(INewsArticleRepository articles, INewsReadingRepository readings,
    NewsSettings settings) : IRequestHandler<GetNewsArticlesQuery, NewsArticleListDto>
{
    public async Task<NewsArticleListDto> Handle(GetNewsArticlesQuery request, CancellationToken ct)
    {
        var filter = new NewsArticleFilter(request.Stage, request.Topic, request.Q?.Trim(), request.Length,
            request.State, request.Page, request.PageSize);
        var (items, total) = await articles.GetPublishedPageAsync(filter, request.LearnerId, ct);
        var facets = await articles.GetFacetsAsync(ct);

        IReadOnlyDictionary<Guid, NewsCardProgress> progress = new Dictionary<Guid, NewsCardProgress>();
        var counts = (Done: 0, Reading: 0);
        if (request.LearnerId != Guid.Empty)
        {
            progress = await readings.GetProgressAsync(request.LearnerId, items.Select(x => x.Id).ToArray(), ct);
            counts = await readings.CountAsync(request.LearnerId, ct);
        }
        var cards = items.Select(x => x.ToCardDto(settings.CoverUrl(x.CoverFile), progress.GetValueOrDefault(x.Id)))
            .ToList();
        return new NewsArticleListDto(cards, total, request.Page, request.PageSize,
            new NewsFacetsDto(facets.Stage, facets.Topic), counts.Done, counts.Reading);
    }
}
