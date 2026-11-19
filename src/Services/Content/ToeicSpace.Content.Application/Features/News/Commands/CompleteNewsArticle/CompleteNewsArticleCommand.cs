using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.News;

namespace ToeicSpace.Content.Application.Features.News.Commands.CompleteNewsArticle;

/// <summary>"Hoàn thành bài đọc": marks the article as read (once; no reward).</summary>
public sealed record CompleteNewsArticleCommand(string Slug, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<NewsReadingStateDto>;
