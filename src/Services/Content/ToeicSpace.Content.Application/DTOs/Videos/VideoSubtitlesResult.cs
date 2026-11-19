namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>Either the subtitles (200) or the processing notice (202).</summary>
public sealed record VideoSubtitlesResult(VideoSubtitlesDto? Subtitles, VideoSubtitlesProcessingDto? Processing);
