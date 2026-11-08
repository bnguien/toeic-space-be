using ToeicSpace.Content.Application.DTOs.News;

namespace ToeicSpace.Content.Application.Features.NewsImport.Commands.ImportNewsArticles;

/// <summary>
/// Idempotent upsert of articles (by slug) from <c>articles.json</c> / <c>.json.gz</c> documents and storage of
/// <c>&lt;hash&gt;.webp</c> covers, in any combination per call.
/// </summary>
public sealed record ImportNewsArticlesCommand(IReadOnlyList<NewsImportFile> Files) : IRequest<NewsImportResultDto>
{
    public const int MaxFiles = 400;
    public const long MaxRequestBytes = 64L * 1024 * 1024;
}
