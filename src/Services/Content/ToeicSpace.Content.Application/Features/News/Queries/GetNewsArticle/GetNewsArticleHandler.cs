using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticle;

public sealed class GetNewsArticleHandler(INewsArticleRepository articles, NewsSettings settings)
    : IRequestHandler<GetNewsArticleQuery, NewsArticleDetailDto>
{
    public async Task<NewsArticleDetailDto> Handle(GetNewsArticleQuery request, CancellationToken ct)
    {
        var article = await articles.GetPublishedBySlugAsync(request.Slug, ct) ?? throw NewsErrors.ArticleNotFound();
        return article.ToDetailDto(settings.CoverUrl(article.CoverFile));
    }
}
