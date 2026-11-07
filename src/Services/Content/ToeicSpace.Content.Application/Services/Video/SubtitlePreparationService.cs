using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// Background job body (Studychill <c>video_subs.prepare</c>): download missing tracks from YouTube, translate
/// Vietnamese from the source track when YouTube has none, then fix Vietnamese lines that are still English. Runs
/// outside requests (20–80 s); the queue guarantees one job per (video, languages).
/// </summary>
public sealed class SubtitlePreparationService(SubtitleStore store, IYouTubeSubtitleClient youtube,
    SubtitleTranslator translator, IVideoCache cache, ILogger<SubtitlePreparationService> logger)
{
    public const string Vietnamese = "vi";
    public static readonly TimeSpan ViFixCooldown = TimeSpan.FromHours(6);
    public static readonly TimeSpan ViCheckedTtl = TimeSpan.FromDays(30);

    /// <summary>True when at least one requested language has cues afterwards.</summary>
    public async Task<bool> PrepareAsync(string videoId, IReadOnlyList<string> langs, CancellationToken ct)
    {
        var cues = await store.ReadCuesAsync(videoId, langs, ct);
        foreach (var lang in langs.Where(l => !cues.ContainsKey(l)))
        {
            var track = await youtube.GetTrackAsync(videoId, lang, ct);
            if (track.Cues.Count == 0)
            {
                logger.LogInformation("Subtitles {VideoId}/{Lang} not downloaded: {Error}", videoId, lang, track.Error);
                continue;
            }
            cues[lang] = track.Cues;
            await store.SaveTrackAsync(videoId, lang, track.Cues, SubtitleTrackSource.YouTube, ct);
        }

        // No Vietnamese track on YouTube (its auto-translation is rate limited hard): translate it ourselves.
        if (langs.Contains(Vietnamese) && !cues.ContainsKey(Vietnamese)
            && SourceLang(langs, cues) is { } sourceLang)
        {
            var translated = await TranslateSafelyAsync(videoId, cues[sourceLang], ct);
            if (translated.Count > 0)
            {
                cues[Vietnamese] = translated;
                await store.SaveTrackAsync(videoId, Vietnamese, translated, SubtitleTrackSource.Translated, ct);
                logger.LogInformation("Subtitles {VideoId}: translated {Count} lines to Vietnamese", videoId, translated.Count);
            }
        }

        if (cues.TryGetValue(Vietnamese, out var vi) && SourceLang(langs, cues) is { } source)
            await FixVietnameseAsync(videoId, vi, cues[source], ct);
        return cues.Values.Any(c => c.Count > 0);
    }

    /// <summary>Fix job for a video whose Vietnamese track is already stored.</summary>
    public async Task FixStoredVietnameseAsync(string videoId, string sourceLang, CancellationToken ct)
    {
        var cues = await store.ReadCuesAsync(videoId, [Vietnamese, sourceLang], ct);
        if (cues.TryGetValue(Vietnamese, out var vi) && cues.TryGetValue(sourceLang, out var source))
            await FixVietnameseAsync(videoId, vi, source, ct);
    }

    public static string ViCheckedKey(string videoId) => $"video:vi-checked:{videoId}";
    public static string ViFailedKey(string videoId) => $"video:vi-fix-failed:{videoId}";

    /// <summary>Re-translates Vietnamese lines that are still English; keeps the track when that fails.</summary>
    public async Task<IReadOnlyList<SubtitleCue>> FixVietnameseAsync(string videoId, IReadOnlyList<SubtitleCue> vi,
        IReadOnlyList<SubtitleCue> en, CancellationToken ct)
    {
        if (cache.TryGet<bool>(ViCheckedKey(videoId), out _) || cache.TryGet<bool>(ViFailedKey(videoId), out _)) return vi;
        var failures = ViEnglishLineDetector.Find(vi, en);
        if (failures.Count == 0)
        {
            cache.Set(ViCheckedKey(videoId), true, ViCheckedTtl);
            return vi;
        }

        var byStart = new Dictionary<double, SubtitleCue>();
        foreach (var cue in en) byStart[cue.Start] = cue;
        var pairs = failures
            .Select(i => (Index: i, Source: ViEnglishLineDetector.MatchingSource(vi, en, byStart, vi.Count == en.Count, i)))
            .Where(x => x.Source is not null).ToList();
        var translated = await TranslateSafelyAsync(videoId, pairs.Select(x => x.Source!).ToList(), ct);
        var updated = vi.ToList();
        var fixedCount = 0;
        for (var k = 0; k < pairs.Count; k++)
        {
            var text = k < translated.Count ? translated[k].Text : string.Empty;
            var (i, source) = pairs[k];
            if (text.Length == 0 || Same(text, vi[i].Text) || Same(text, source!.Text)) continue;
            updated[i] = vi[i] with { Text = text };
            fixedCount++;
        }
        cache.Set(fixedCount < failures.Count ? ViFailedKey(videoId) : ViCheckedKey(videoId), true,
            fixedCount < failures.Count ? ViFixCooldown : ViCheckedTtl);
        if (fixedCount == 0) return vi;
        await store.SaveTrackAsync(videoId, Vietnamese, updated, SubtitleTrackSource.Translated, ct);
        logger.LogInformation("Subtitles {VideoId}: re-translated {Fixed}/{Total} Vietnamese lines still in English",
            videoId, fixedCount, failures.Count);
        return updated;
    }

    private async Task<IReadOnlyList<SubtitleCue>> TranslateSafelyAsync(string videoId, IReadOnlyList<SubtitleCue> source,
        CancellationToken ct)
    {
        try { return await translator.TranslateAsync(source, ct); }
        catch (Exception ex) when (ex is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            logger.LogWarning("Subtitle translation of {VideoId} failed: {Error}", videoId, ex.GetType().Name);
            return [];
        }
    }

    private static string? SourceLang(IReadOnlyList<string> langs, IReadOnlyDictionary<string, IReadOnlyList<SubtitleCue>> cues) =>
        langs.FirstOrDefault(l => l != Vietnamese && cues.TryGetValue(l, out var c) && c.Count > 0);

    private static bool Same(string a, string b) =>
        string.Join(' ', a.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        == string.Join(' ', b.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
