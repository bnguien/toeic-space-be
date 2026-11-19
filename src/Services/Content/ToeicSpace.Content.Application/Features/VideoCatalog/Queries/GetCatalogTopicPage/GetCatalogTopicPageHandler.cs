using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogTopicPage;

public sealed class GetCatalogTopicPageHandler(IVideoCatalogRepository catalog)
    : IRequestHandler<GetCatalogTopicPageQuery, CatalogTopicPageDto>
{
    public async Task<CatalogTopicPageDto> Handle(GetCatalogTopicPageQuery request, CancellationToken ct)
    {
        var title = await catalog.GetTopicTitleAsync(request.Slug, ct) ?? throw VideoErrors.TopicNotFound();
        var limit = Math.Clamp(request.Limit, 1, GetCatalogTopicPageQuery.MaxLimit);
        var offset = Math.Max(0, request.Offset);
        var rows = await catalog.GetVisibleTopicVideosAsync(request.Slug, offset, limit + 1, ct);
        return new CatalogTopicPageDto(request.Slug, title, rows.Take(limit).Select(r => r.ToDto()).ToList(),
            rows.Count > limit, offset + Math.Min(limit, rows.Count));
    }
}
