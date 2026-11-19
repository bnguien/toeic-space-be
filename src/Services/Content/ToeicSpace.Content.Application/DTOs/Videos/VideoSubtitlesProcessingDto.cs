namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>202 body while subtitles are prepared in the background; poll again after <c>retryAfter</c> seconds.</summary>
public sealed record VideoSubtitlesProcessingDto(string Status, int RetryAfter, string VideoId)
{
    public const int DefaultRetryAfter = 3;
    public static VideoSubtitlesProcessingDto For(string videoId) => new("processing", DefaultRetryAfter, videoId);
}
