using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Interfaces.Repositories;

/// <summary>A shared video group: the first learner who shared it and how many learners share it.</summary>
public sealed record SharedVideoGroup(VideoWatch First, int ShareCount);

public interface IVideoWatchRepository
{
    /// <summary>The learner's row for a video, deleted or not (tracked).</summary>
    Task<VideoWatch?> GetForUpdateAsync(Guid learnerId, string videoId, CancellationToken ct);
    /// <summary>The learner's row by id, not deleted (tracked).</summary>
    Task<VideoWatch?> GetByIdForUpdateAsync(Guid learnerId, Guid id, CancellationToken ct);
    /// <summary>Not deleted, most recently opened first.</summary>
    Task<IReadOnlyList<VideoWatch>> GetMineAsync(Guid learnerId, int take, CancellationToken ct);
    /// <summary>Public videos of other learners grouped by video (excluding <paramref name="excludeVideoIds"/>).</summary>
    Task<IReadOnlyList<SharedVideoGroup>> GetSharedAsync(Guid learnerId, IReadOnlyCollection<string> excludeVideoIds,
        int take, CancellationToken ct);
    /// <summary>Other learners' public, not deleted rows of one video, first shared first.</summary>
    Task<IReadOnlyList<VideoWatch>> GetOtherSharersAsync(Guid learnerId, string videoId, CancellationToken ct);
    /// <summary>Rows with progress, newest progress first.</summary>
    Task<IReadOnlyList<VideoWatch>> GetProgressAsync(Guid learnerId, int take, CancellationToken ct);
    void Add(VideoWatch watch);
}
