using ToeicSpace.Content.Application.DTOs.News;

namespace ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticle;

/// <summary>A published article with its body and vocabulary.</summary>
public sealed record GetNewsArticleQuery(string Slug) : IRequest<NewsArticleDetailDto>;
