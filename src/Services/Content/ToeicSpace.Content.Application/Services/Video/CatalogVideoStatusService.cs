using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// What the server learns about catalog videos while learners use them: English subtitles or not, subtitle-based
/// level, embeddable or not. Every visible change clears the public catalog cache. Non-catalog videos are a no-op.
/// </summary>
public sealed class CatalogVideoStatusService(IVideoCatalogRepository catalog, IUnitOfWork unitOfWork,
    VideoCatalogCache catalogCache, VideoWordLevelProvider wordLevels, TimeProvider clock,
    ILogger<CatalogVideoStatusService> logger)
{
    private static readonly JsonSerializerOptions MetricsJson = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    /// <summary>Only for certain results (subtitles obtained, or YouTube listed no English) — never for 429/503.</summary>
    public async Task<bool> MarkSubtitlesAsync(string videoId, bool hasEnglish, CancellationToken ct)
    {
        var video = await catalog.GetForUpdateAsync(videoId, ct);
        var status = hasEnglish ? VideoSubtitleStatus.Available : VideoSubtitleStatus.Missing;
        if (video is null || video.SubtitleStatus == status) return false;
        var now = clock.GetUtcNow().UtcDateTime;
        video.SubtitleStatus = status;
        video.SubtitleCheckedAt = now;
        video.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Catalog video {VideoId} subtitleStatus={Status}", videoId, status);
        catalogCache.Clear();
        return true;
    }

    /// <summary>Scores the level from the English subtitles just served when it is still a channel estimate.</summary>
    public async Task<VideoLevel?> ApplyLevelIfNeededAsync(string videoId, IReadOnlyList<SubtitleCue> cues,
        CancellationToken ct)
    {
        if (cues.Count == 0) return null;
        var video = await catalog.GetForUpdateAsync(videoId, ct);
        if (video is null || video.LevelSource != VideoLevelSource.Estimate) return null;
        var measurements = VideoLevelGrader.Measure(cues, wordLevels.Table);
        if (measurements.Words == 0) return null;
        var level = VideoLevelGrader.Grade(measurements, video.Duration, video.Level);
        var now = clock.GetUtcNow().UtcDateTime;
        video.Difficulty = level.Difficulty;
        video.Cefr = level.Cefr;
        video.Track = level.Track;
        video.LevelSource = VideoLevelSource.Subtitles;
        video.LevelMetrics = JsonSerializer.Serialize(measurements, MetricsJson);
        video.LevelScoredAt = now;
        video.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        logger.LogInformation("Catalog video {VideoId} level {Difficulty} {Cefr} ({Track})", videoId, level.Difficulty,
            level.Cefr, level.Track);
        catalogCache.Clear();
        return level;
    }

    /// <summary>Records an embed check; true when the video's visibility changed (hidden ↔ shown).</summary>
    public async Task<bool> MarkEmbedAsync(string videoId, EmbedCheck check, CancellationToken ct)
    {
        var video = await catalog.GetForUpdateAsync(videoId, ct);
        if (video is null) return false;
        var wasBlocked = video.EmbedStatus == VideoEmbedStatus.Blocked;
        var now = clock.GetUtcNow().UtcDateTime;
        video.EmbedStatus = check.Status;
        video.EmbedReason = check.Reason.Length > 16 ? check.Reason[..16] : check.Reason;
        video.EmbedCheckedAt = now;
        video.UpdatedAt = now;
        await unitOfWork.SaveChangesAsync(ct);
        var changed = wasBlocked != (check.Status == VideoEmbedStatus.Blocked);
        if (changed)
        {
            logger.LogInformation("Catalog video {VideoId} embedStatus={Status} ({Reason})", videoId, check.Status,
                check.Reason);
            catalogCache.Clear();
        }
        return changed;
    }
}
