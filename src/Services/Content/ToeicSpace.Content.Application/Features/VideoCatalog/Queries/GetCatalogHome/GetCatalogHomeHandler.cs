using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Services.Video;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogHome;

public sealed class GetCatalogHomeHandler(IVideoCatalogRepository catalog, VideoCatalogCache catalogCache)
    : IRequestHandler<GetCatalogHomeQuery, CatalogHomeDto>
{
    public const int PageSize = 12;

    public Task<CatalogHomeDto> Handle(GetCatalogHomeQuery request, CancellationToken ct) =>
        catalogCache.GetOrCreateAsync("home", VideoCatalogCache.HomeTtl, async () =>
        {
            var rows = await catalog.GetVisibleVideosAsync(ct);
            var topics = rows.GroupBy(r => r.TopicSlug)
                .OrderBy(g => g.First().TopicPosition).ThenBy(g => g.Key, StringComparer.Ordinal)
                .Select(g =>
                {
                    var ordered = CatalogRowOrder.Apply(g).ToList();
                    var tracks = VideoMappingExtensions.TrackKeys.ToDictionary(k => k,
                        k => g.Count(r => r.Track.Key() == k));
                    return new CatalogTopicDto(g.Key, g.First().TopicTitle, tracks,
                        ordered.Take(PageSize).Select(r => r.ToDto()).ToList(), ordered.Count > PageSize,
                        Math.Min(PageSize, ordered.Count));
                })
                .Where(t => t.Videos.Count > 0)
                .ToList();
            return new CatalogHomeDto(topics);
        });
}
