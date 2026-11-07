namespace ToeicSpace.Content.Application.Interfaces.Services.Video;

/// <summary>Outcome of the last background preparation of (video, languages): any cues obtained.</summary>
public sealed record SubtitlePrepareOutcome(bool Ok);

/// <summary>
/// Background subtitle preparation (YouTube download, Vietnamese machine translation, fixing Vietnamese lines that are
/// still English). Single-flight: one job per (video, sorted languages) however many learners ask.
/// </summary>
public interface IVideoSubtitlePreparer
{
    public static readonly TimeSpan OkOutcomeTtl = TimeSpan.FromHours(6);
    public static readonly TimeSpan FailedOutcomeTtl = TimeSpan.FromSeconds(60);
    public static readonly TimeSpan LockTtl = TimeSpan.FromSeconds(300);

    /// <summary>Queues a preparation job; false when one is already queued/running for the same key.</summary>
    bool RequestPrepare(string videoId, IReadOnlyList<string> langs);
    SubtitlePrepareOutcome? GetOutcome(string videoId, IReadOnlyList<string> langs);
    /// <summary>Queues the check/fix of Vietnamese lines that are still English (once per video).</summary>
    bool RequestViFix(string videoId, string sourceLang);
}
