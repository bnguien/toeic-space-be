namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>A catalog card: subtitleStatus unknown|available|missing, track easy|medium|hard|"", levelSource estimate|subtitles.</summary>
public sealed record CatalogVideoDto(string VideoId, string Title, int Duration, string Level, string ChannelTitle,
    string Thumbnail, string SubtitleStatus, int Difficulty, string Cefr, string Track, string LevelSource);
