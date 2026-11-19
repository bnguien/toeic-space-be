using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogHome;

/// <summary>Row order inside a topic: videos with known subtitles first (open instantly), then position.</summary>
public static class CatalogRowOrder
{
    public static IEnumerable<CatalogVideoRow> Apply(IEnumerable<CatalogVideoRow> rows) =>
        rows.OrderBy(r => r.SubtitleStatus == VideoSubtitleStatus.Available ? 0 : 1).ThenBy(r => r.Position)
            .ThenBy(r => r.VideoId, StringComparer.Ordinal);
}
