namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One learner's record of one video (table <c>VideoWatches</c>): "Video của tôi" (open count, soft delete), public
/// sharing ("Video tham khảo") and watch progress (resume position, furthest percent, completion). Title and
/// duration are snapshots taken when it was opened so cards render without calling YouTube.
/// </summary>
public sealed class VideoWatch
{
    public const int CompletePercent = 90;
    public const int MaxSeconds = 24 * 3600;
    public const int SharerNameMaxLength = 80;
    public const int SharerAvatarMaxLength = 300;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public string VideoId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int Duration { get; set; }
    public string Language { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    /// <summary>Display name shown on the shared card (the access token carries no name; the client sends it).</summary>
    public string? SharerName { get; set; }
    public string? SharerAvatar { get; set; }
    public int OpenCount { get; set; } = 1;
    public DateTime? LastOpenedAt { get; set; }
    public int LastPosition { get; set; }
    public int WatchedPercent { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? ProgressAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
