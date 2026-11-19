using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Features.VideoSubtitles.Queries.GetVideoSubtitles;

/// <summary>
/// Studychill <c>video_subs</c>: codes the video does not have are dropped (<c>vi</c> is always kept because the server
/// translates it); only the store is read in the request; anything missing queues ONE background job per
/// (video, languages) and answers 202; once the job ran, what exists is returned (empty → 503
/// <c>upstream_unavailable</c>). English cues mark the catalog video as having subtitles and score its level.
/// </summary>
public sealed class GetVideoSubtitlesHandler(SubtitleStore store, IVideoSubtitlePreparer preparer, VideoQuota quota,
    CatalogVideoStatusService status, VideoWordLevelProvider wordLevels, ILogger<GetVideoSubtitlesHandler> logger)
    : IRequestHandler<GetVideoSubtitlesQuery, VideoSubtitlesResult>
{
    public async Task<VideoSubtitlesResult> Handle(GetVideoSubtitlesQuery request, CancellationToken ct)
    {
        var videoId = request.VideoId;
        var langs = request.LangList.Where(VideoLinks.IsValidLang).ToList();
        var unavailable = new List<string>();
        var languages = await store.GetLanguagesAsync(videoId, ct);
        if (languages is not null)
        {
            var codes = SubtitleLanguageRules.LanguageCodes(languages.Manual, languages.Auto)
                .Select(c => c.ToLowerInvariant()).ToHashSet();
            unavailable = langs.Where(l => !codes.Contains(l.ToLowerInvariant()) && l != SubtitlePreparationService.Vietnamese)
                .ToList();
            langs = langs.Except(unavailable).ToList();
            if (langs.Count == 0)
                throw VideoErrors.LangUnavailable(unavailable,
                    SubtitleLanguageRules.PickOriginalLanguage(languages.OriginalLanguage, languages.Manual, languages.Auto));
        }

        var cues = await store.ReadCuesAsync(videoId, langs, ct);
        if (langs.Any(l => !cues.ContainsKey(l)))
        {
            if (preparer.GetOutcome(videoId, langs) is null)
            {
                if (!quota.TryConsume(request.LearnerId, videoId)) throw VideoErrors.QuotaExceeded();
                preparer.RequestPrepare(videoId, langs);
                return new VideoSubtitlesResult(null, VideoSubtitlesProcessingDto.For(videoId));
            }
            cues = await store.ReadCuesAsync(videoId, langs, ct);
        }
        var result = langs.ToDictionary(l => l, l => cues.TryGetValue(l, out var c) ? c : (IReadOnlyList<SubtitleCue>)[]);

        if (result.TryGetValue(SubtitlePreparationService.Vietnamese, out var vi) && vi.Count > 0
            && langs.FirstOrDefault(l => l != SubtitlePreparationService.Vietnamese && result[l].Count > 0) is { } source)
            preparer.RequestViFix(videoId, source);

        if (result.Values.All(c => c.Count == 0)) throw VideoErrors.UpstreamUnavailable(unavailable);

        var english = langs.FirstOrDefault(l => VideoLinks.IsEnglish(l) && result[l].Count > 0);
        IReadOnlyDictionary<string, string> levels = new Dictionary<string, string>();
        if (english is not null)
        {
            await status.MarkSubtitlesAsync(videoId, hasEnglish: true, ct);
            try { await status.ApplyLevelIfNeededAsync(videoId, result[english], ct); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Scoring the level of {VideoId} failed", videoId);
            }
            levels = wordLevels.TranscriptLevels(videoId, english, result[english]);
        }
        return new VideoSubtitlesResult(new VideoSubtitlesDto(videoId,
            result.ToDictionary(x => x.Key, x => x.Value), result.ToDictionary(x => x.Key, x => x.Value.Count),
            unavailable, levels), null);
    }
}
