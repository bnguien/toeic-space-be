using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogPath;

public sealed class GetCatalogPathHandler(IVideoCatalogRepository catalog, VideoCatalogCache catalogCache)
    : IRequestHandler<GetCatalogPathQuery, CatalogPathDto>
{
    public Task<CatalogPathDto> Handle(GetCatalogPathQuery request, CancellationToken ct)
    {
        var track = string.IsNullOrWhiteSpace(request.Track) ? "easy" : request.Track.Trim().ToLowerInvariant();
        return catalogCache.GetOrCreateAsync($"path:{track}", VideoCatalogCache.PathTtl, async () =>
        {
            var rows = await catalog.GetVisibleVideosAsync(ct);
            var totals = VideoMappingExtensions.TrackKeys.ToDictionary(k => k, k => rows.Count(r => r.Track.Key() == k));
            var ordered = CatalogPathOrdering.Order(rows.Where(r => r.Track.Key() == track), r => r.Difficulty,
                r => r.Duration, r => r.VideoId, r => r.TopicSlug);
            return new CatalogPathDto(track, totals,
                ordered.Take(CatalogPathOrdering.MaxVideos).Select(r => r.ToPathDto()).ToList());
        });
    }
}
