namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>A listening-path step: catalog card fields plus its topic.</summary>
public sealed record CatalogPathVideoDto(string VideoId, string Title, int Duration, string Level, string ChannelTitle,
    string Thumbnail, string SubtitleStatus, int Difficulty, string Cefr, string Track, string LevelSource, string Topic,
    string TopicTitle);
