using ToeicSpace.Content.Application.DTOs.Videos;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogTopicPage;

/// <summary>Next page of one topic row when the learner scrolls to its end.</summary>
public sealed record GetCatalogTopicPageQuery(string Slug, int Offset = 0, int Limit = 12) : IRequest<CatalogTopicPageDto>
{
    public const int MaxLimit = 30;
}
