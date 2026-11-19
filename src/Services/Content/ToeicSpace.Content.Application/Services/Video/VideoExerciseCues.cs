using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// The cleaned sentence an exercise refers to: the learner must have opened the video, and cue indexes count the
/// cleaned cues exactly like the reader (Studychill <c>clean_cues</c>), so the browser and the server agree.
/// </summary>
public sealed class VideoExerciseCues(IVideoWatchRepository watches, SubtitleStore store)
{
    public async Task<string> SentenceAsync(Guid learnerId, string videoId, string lang, int cueIndex, CancellationToken ct)
    {
        var row = await watches.GetForUpdateAsync(learnerId, videoId, ct);
        if (row is null || row.IsDeleted) throw VideoErrors.WatchedVideoNotFound();
        var cues = await store.ReadCuesAsync(videoId, [lang], ct);
        var cleaned = TranscriptText.CleanCues(cues.GetValueOrDefault(lang) ?? []);
        if (cueIndex < 0 || cueIndex >= cleaned.Count) throw VideoErrors.CueNotFound();
        return cleaned[cueIndex].Text;
    }
}
