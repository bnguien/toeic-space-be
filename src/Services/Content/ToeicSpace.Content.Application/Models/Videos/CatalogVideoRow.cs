using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>Read projection of a visible catalog video.</summary>
public sealed record CatalogVideoRow(string VideoId, string TopicSlug, string TopicTitle, int TopicPosition, string Title,
    int Duration, string Level, string ChannelTitle, string Thumbnail, VideoSubtitleStatus SubtitleStatus, int Difficulty,
    string Cefr, VideoTrack Track, VideoLevelSource LevelSource, int Position);
