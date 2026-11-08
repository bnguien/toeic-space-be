using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// A curated YouTube video shared by every learner (table <c>VideoCatalogItems</c>). Metadata only: subtitles live in
/// <see cref="VideoSubtitle"/>. The topic is stored on the row (8 fixed topics). <see cref="SubtitleStatus"/> and
/// <see cref="EmbedStatus"/> record what the server learned so unusable videos leave the catalog; <see cref="IsActive"/>
/// is the admin switch.
/// </summary>
public sealed class VideoCatalogItem
{
    public const int VideoIdMaxLength = 32;
    public const int TopicSlugMaxLength = 40;
    public const int TopicTitleMaxLength = 120;
    public const int TitleMaxLength = 300;
    public const int ChannelHandleMaxLength = 80;
    public const int ChannelTitleMaxLength = 120;
    public const int ThumbnailMaxLength = 300;
    public const int EmbedReasonMaxLength = 16;

    public Guid Id { get; set; } = Guid.NewGuid();
    public string VideoId { get; set; } = string.Empty;
    public string TopicSlug { get; set; } = string.Empty;
    public string TopicTitle { get; set; } = string.Empty;
    /// <summary>Row order of the topic on the catalog page.</summary>
    public int TopicPosition { get; set; }
    public string Title { get; set; } = string.Empty;
    public string ChannelHandle { get; set; } = string.Empty;
    public string ChannelTitle { get; set; } = string.Empty;
    /// <summary>Seconds.</summary>
    public int Duration { get; set; }
    /// <summary>Suggested CEFR level of the channel.</summary>
    public string Level { get; set; } = string.Empty;
    public string Thumbnail { get; set; } = string.Empty;
    public long ViewCount { get; set; }
    /// <summary>Order inside the topic.</summary>
    public int Position { get; set; }
    public bool IsActive { get; set; } = true;
    public VideoSubtitleStatus SubtitleStatus { get; set; }
    public DateTime? SubtitleCheckedAt { get; set; }
    /// <summary>Difficulty score 0–100.</summary>
    public int Difficulty { get; set; }
    public string Cefr { get; set; } = string.Empty;
    public VideoTrack Track { get; set; }
    public VideoLevelSource LevelSource { get; set; }
    /// <summary>Subtitle measurements (JSON, camelCase) behind the level when scored from subtitles.</summary>
    public string? LevelMetrics { get; set; }
    public DateTime? LevelScoredAt { get; set; }
    public VideoEmbedStatus EmbedStatus { get; set; }
    public string EmbedReason { get; set; } = string.Empty;
    public DateTime? EmbedCheckedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Listed in the catalog: admin switch on, English subtitles not ruled out, embeddable.</summary>
    public bool IsVisible => IsActive && SubtitleStatus != VideoSubtitleStatus.Missing
        && EmbedStatus != VideoEmbedStatus.Blocked;
}
