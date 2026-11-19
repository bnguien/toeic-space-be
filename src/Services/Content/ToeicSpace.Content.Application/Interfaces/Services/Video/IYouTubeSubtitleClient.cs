using ToeicSpace.Content.Application.Models.Videos;

namespace ToeicSpace.Content.Application.Interfaces.Services.Video;

/// <summary>YouTube access (language list, title, duration, caption tracks). Never throws except on cancellation.</summary>
public interface IYouTubeSubtitleClient
{
    Task<YouTubeLanguagesResult> GetLanguagesAsync(string videoId, CancellationToken ct);

    /// <summary>
    /// Cues of one language: <c>xx-orig</c> = the automatic (speech recognition) track of language xx, otherwise the
    /// uploaded track, otherwise the automatic one. Per-word offsets are kept when YouTube provides them.
    /// </summary>
    Task<YouTubeTrackResult> GetTrackAsync(string videoId, string lang, CancellationToken ct);
}
