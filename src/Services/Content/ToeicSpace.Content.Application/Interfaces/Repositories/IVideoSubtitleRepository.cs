using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>Subtitle documents, one per YouTube video.</summary>
public interface IVideoSubtitleRepository
{
    Task<VideoSubtitle?> GetAsync(string videoId, CancellationToken ct);
    Task<VideoSubtitle?> GetForUpdateAsync(string videoId, CancellationToken ct);
    Task<IReadOnlyDictionary<string, VideoSubtitle>> GetManyForUpdateAsync(IReadOnlyCollection<string> videoIds,
        CancellationToken ct);
    void Add(VideoSubtitle subtitle);
}
