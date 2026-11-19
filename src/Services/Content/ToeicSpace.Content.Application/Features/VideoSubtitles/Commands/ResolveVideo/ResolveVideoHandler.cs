using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Interfaces.Services.Video;
using ToeicSpace.Content.Application.Mappings;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Application.Services.Video;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules.Videos;

namespace ToeicSpace.Content.Application.Features.VideoSubtitles.Commands.ResolveVideo;

/// <summary>
/// Studychill <c>video_resolve</c>: language list from the store, else from YouTube (counted in the 12 new videos per
/// hour quota). No subtitles at all → <c>no_english</c> for a catalog video (hidden unless the error was temporary)
/// or <c>video_unavailable</c>; a catalog video without real English → <c>no_english</c>. Then the learner's row is
/// created or reopened.
/// </summary>
public sealed class ResolveVideoHandler(SubtitleStore store, IYouTubeSubtitleClient youtube, VideoQuota quota,
    IVideoCatalogRepository catalog, CatalogVideoStatusService status, IVideoWatchRepository watches,
    IUnitOfWork unitOfWork, TimeProvider clock, ILogger<ResolveVideoHandler> logger)
    : IRequestHandler<ResolveVideoCommand, VideoResolveDto>
{
    public async Task<VideoResolveDto> Handle(ResolveVideoCommand request, CancellationToken ct)
    {
        var videoId = VideoLinks.ExtractVideoId(request.Url);
        if (!VideoLinks.IsValidVideoId(videoId)) throw VideoErrors.InvalidVideoUrl();

        var languages = await store.GetLanguagesAsync(videoId, ct);
        string? error = null;
        if (languages is null)
        {
            if (!quota.TryConsume(request.LearnerId, videoId)) throw VideoErrors.QuotaExceeded();
            var fetched = await youtube.GetLanguagesAsync(videoId, ct);
            if (fetched.Error is null) languages = await store.SaveLanguagesAsync(videoId, fetched, ct);
            else error = fetched.Error;
        }

        var manual = languages?.Manual ?? [];
        var auto = languages?.Auto ?? [];
        var inCatalog = await catalog.ExistsAsync(videoId, ct);
        if (manual.Count == 0 && auto.Count == 0)
        {
            // A temporary failure (network, 429) must not hide a catalog video; a removed/blocked one must.
            if (inCatalog && (error is null || SubtitleLanguageRules.IsPermanentError(error)))
            {
                await status.MarkSubtitlesAsync(videoId, hasEnglish: false, ct);
                throw VideoErrors.NoEnglish(videoId);
            }
            throw VideoErrors.VideoUnavailable(videoId);
        }
        // Catalog videos exist to learn English; pasted links may be any language on purpose.
        if (inCatalog && !SubtitleLanguageRules.HasEnglish(manual, auto, languages!.OriginalLanguage))
        {
            await status.MarkSubtitlesAsync(videoId, hasEnglish: false, ct);
            throw VideoErrors.NoEnglish(videoId);
        }

        var original = SubtitleLanguageRules.PickOriginalLanguage(languages!.OriginalLanguage, manual, auto);
        await RecordOpenAsync(request.LearnerId, videoId, languages, original, ct);
        return new VideoResolveDto(videoId, languages.Title, languages.Duration, original,
            manual.Select(x => x.ToDto()).ToList(), auto.Select(x => x.ToDto()).ToList());
    }

    private async Task RecordOpenAsync(Guid learnerId, string videoId, StoredLanguages languages, string original,
        CancellationToken ct)
    {
        try
        {
            var now = clock.GetUtcNow().UtcDateTime;
            var row = await watches.GetForUpdateAsync(learnerId, videoId, ct);
            if (row is null)
            {
                watches.Add(new VideoWatch
                {
                    LearnerId = learnerId, VideoId = videoId, Title = Cut(languages.Title, VideoCatalogItem.TitleMaxLength),
                    Duration = languages.Duration, Language = Cut(original, VideoSubtitle.LanguageMaxLength),
                    LastOpenedAt = now, CreatedAt = now, UpdatedAt = now
                });
            }
            else
            {
                row.OpenCount++;
                row.LastOpenedAt = now;
                // The video may have been renamed on YouTube.
                if (languages.Title.Length > 0) row.Title = Cut(languages.Title, VideoCatalogItem.TitleMaxLength);
                if (languages.Duration > 0) row.Duration = languages.Duration;
                if (original.Length > 0) row.Language = Cut(original, VideoSubtitle.LanguageMaxLength);
                row.IsDeleted = false;
                row.UpdatedAt = now;
            }
            await unitOfWork.SaveChangesAsync(ct);
        }
        catch (ContentRuleException ex)
        {
            // Recording "Video của tôi" must never prevent opening the video.
            logger.LogWarning("Recording opened video {VideoId} failed: {Code}", videoId, ex.Code);
        }
    }

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];
}
