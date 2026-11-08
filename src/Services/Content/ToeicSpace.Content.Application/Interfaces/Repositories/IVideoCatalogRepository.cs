using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

public interface IVideoCatalogRepository
{
    /// <summary>Every visible video (admin switch on, English not ruled out, embeddable).</summary>
    Task<IReadOnlyList<CatalogVideoRow>> GetVisibleVideosAsync(CancellationToken ct);
    /// <summary>Visible videos of one topic in row order (subtitles known first, then position).</summary>
    Task<IReadOnlyList<CatalogVideoRow>> GetVisibleTopicVideosAsync(string topicSlug, int offset, int take,
        CancellationToken ct);
    /// <summary>Title of a topic that has at least one active video; null when unknown.</summary>
    Task<string?> GetTopicTitleAsync(string topicSlug, CancellationToken ct);
    Task<bool> ExistsAsync(string videoId, CancellationToken ct);
    Task<VideoCatalogItem?> GetForUpdateAsync(string videoId, CancellationToken ct);
    Task<IReadOnlyDictionary<string, VideoCatalogItem>> GetManyForUpdateAsync(IReadOnlyCollection<string> videoIds,
        CancellationToken ct);
    void Add(VideoCatalogItem item);
}
