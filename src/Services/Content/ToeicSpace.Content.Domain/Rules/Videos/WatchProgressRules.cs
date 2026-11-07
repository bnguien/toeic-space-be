using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>
/// Watch progress (Studychill <c>watch_progress.py</c>): the resume position follows the player, the watched percent
/// only grows (seeking back never loses "watched"), ≥ 90% completes the video once.
/// </summary>
public static class WatchProgressRules
{
    public static void Apply(VideoWatch video, double position, double duration, DateTime now)
    {
        var pos = (int)Math.Max(0, Math.Min(position, VideoWatch.MaxSeconds));
        var dur = (int)Math.Max(0, Math.Min(duration, VideoWatch.MaxSeconds));
        if (dur == 0) dur = video.Duration;
        if (dur > 0) pos = Math.Min(pos, dur);
        var percent = dur > 0 ? Math.Min(100, (int)Math.Round(pos * 100.0 / dur, MidpointRounding.ToEven)) : 0;
        video.LastPosition = pos;
        video.WatchedPercent = Math.Max(video.WatchedPercent, percent);
        if (dur > 0 && video.Duration == 0) video.Duration = dur;
        if (video.CompletedAt is null && video.WatchedPercent >= VideoWatch.CompletePercent) video.CompletedAt = now;
        video.ProgressAt = now;
        video.UpdatedAt = now;
    }
}
