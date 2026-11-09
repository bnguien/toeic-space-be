using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>One stored track inside <see cref="VideoSubtitle.Tracks"/>.</summary>
public sealed record StoredTrack(SubtitleTrackSource Source, IReadOnlyList<SubtitleCue> Cues);

/// <summary>
/// Reads and writes the subtitle documents (<see cref="VideoSubtitle"/>): the language list JSON
/// <c>{"manual":[…],"auto":[…]}</c> and the tracks JSON <c>{"code":{"source":"imported","cues":[…]}}</c>.
/// </summary>
public sealed class SubtitleStore(IVideoSubtitleRepository subtitles, IUnitOfWork unitOfWork, TimeProvider clock,
    ILogger<SubtitleStore> logger)
{
    /// <summary>camelCase keys, enums as camelCase strings, Vietnamese kept readable (not escaped).</summary>
    public static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private sealed record LanguagesDocument(List<VideoLanguage> Manual, List<VideoLanguage> Auto);

    /// <summary>Non-empty stored tracks for the requested codes (case-insensitive), keyed by the requested spelling.</summary>
    public async Task<Dictionary<string, IReadOnlyList<SubtitleCue>>> ReadCuesAsync(string videoId,
        IReadOnlyCollection<string> langs, CancellationToken ct)
    {
        var row = await subtitles.GetAsync(videoId, ct);
        var result = new Dictionary<string, IReadOnlyList<SubtitleCue>>();
        if (row is null) return result;
        var tracks = ReadTracks(row.Tracks);
        foreach (var lang in langs)
        {
            var hit = tracks.FirstOrDefault(x => string.Equals(x.Key, lang, StringComparison.OrdinalIgnoreCase)).Value;
            if (hit is { Cues.Count: > 0 }) result[lang] = hit.Cues;
        }
        return result;
    }

    public async Task SaveTrackAsync(string videoId, string lang, IReadOnlyList<SubtitleCue> cues,
        SubtitleTrackSource source, CancellationToken ct)
    {
        if (cues.Count == 0) return;
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var row = await GetOrAddAsync(videoId, ct);
            var tracks = ReadTracks(row.Tracks);
            var existing = tracks.Keys.FirstOrDefault(k => string.Equals(k, lang, StringComparison.OrdinalIgnoreCase));
            if (existing is not null) tracks.Remove(existing);
            tracks[lang] = new StoredTrack(source, cues);
            row.Tracks = WriteTracks(tracks);
            row.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            if (await TrySaveAsync(videoId, ct)) return;
        }
    }

    public async Task<StoredLanguages?> GetLanguagesAsync(string videoId, CancellationToken ct)
    {
        var row = await subtitles.GetAsync(videoId, ct);
        return row is null ? null : ReadLanguages(row);
    }

    public async Task<StoredLanguages> SaveLanguagesAsync(string videoId, YouTubeLanguagesResult languages,
        CancellationToken ct)
    {
        for (var attempt = 0; attempt < 2; attempt++)
        {
            var row = await GetOrAddAsync(videoId, ct);
            ApplyLanguages(row, languages.Manual, languages.Auto, languages.Title, languages.Duration, languages.Language);
            row.LanguagesFetchedAt = row.UpdatedAt = clock.GetUtcNow().UtcDateTime;
            if (await TrySaveAsync(videoId, ct)) break;
        }
        return new StoredLanguages(languages.Manual, languages.Auto, languages.Title, languages.Duration, languages.Language);
    }

    public static void ApplyLanguages(VideoSubtitle row, IReadOnlyList<VideoLanguage> manual,
        IReadOnlyList<VideoLanguage> auto, string? title, int duration, string? language)
    {
        row.Languages = JsonSerializer.Serialize(new LanguagesDocument([.. manual], [.. auto]), Json);
        row.Title = Cut(title, VideoCatalogItem.TitleMaxLength);
        row.Duration = Math.Max(0, duration);
        row.OriginalLanguage = Cut(language, VideoSubtitle.LanguageMaxLength);
    }

    /// <summary>The language list of a row; null when the video was never listed.</summary>
    public static StoredLanguages? ReadLanguages(VideoSubtitle row)
    {
        if (string.IsNullOrEmpty(row.Languages)) return null;
        try
        {
            var doc = JsonSerializer.Deserialize<LanguagesDocument>(row.Languages, Json);
            return new StoredLanguages(doc?.Manual ?? [], doc?.Auto ?? [], row.Title, row.Duration, row.OriginalLanguage);
        }
        catch (JsonException) { return null; }
    }

    public static Dictionary<string, StoredTrack> ReadTracks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return JsonSerializer.Deserialize<Dictionary<string, StoredTrack>>(json, Json) ?? []; }
        catch (JsonException) { return []; }
    }

    public static string WriteTracks(IReadOnlyDictionary<string, StoredTrack> tracks) =>
        JsonSerializer.Serialize(tracks.OrderBy(x => x.Key, StringComparer.Ordinal).ToDictionary(x => x.Key, x => x.Value), Json);

    private async Task<VideoSubtitle> GetOrAddAsync(string videoId, CancellationToken ct)
    {
        var row = await subtitles.GetForUpdateAsync(videoId, ct);
        if (row is not null) return row;
        var now = clock.GetUtcNow().UtcDateTime;
        row = new VideoSubtitle { VideoId = videoId, CreatedAt = now, UpdatedAt = now };
        subtitles.Add(row);
        return row;
    }

    private async Task<bool> TrySaveAsync(string videoId, CancellationToken ct)
    {
        try
        {
            await unitOfWork.SaveChangesAsync(ct);
            return true;
        }
        catch (ContentRuleException ex) when (ex.Code is "unique_constraint" or "concurrency_conflict")
        {
            // Another request created the row first: retry once on the stored row.
            logger.LogDebug("Subtitle document {VideoId} stored concurrently", videoId);
            return false;
        }
    }

    private static string Cut(string? value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];
}
