using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.News;

namespace ToeicSpace.Content.Application.Features.News.Commands.OpenNewsArticle;

/// <summary>The reader page opens the article: the first open is recorded, reloading keeps it.</summary>
public sealed record OpenNewsArticleCommand(string Slug, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<NewsReadingStateDto>;
