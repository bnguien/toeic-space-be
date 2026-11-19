using System.Text.Json;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Mappings;

/// <summary>Video entities/models → DTOs, and the lowercase string forms of the video enums used by the API and data files.</summary>
public static class VideoMappingExtensions
{
    public const string DefaultSharerName = "Học viên TOEIC Space";
    public static readonly string[] TrackKeys = ["easy", "medium", "hard"];

    public static string Key(this VideoTrack track) => track switch
    {
        VideoTrack.Easy => "easy",
        VideoTrack.Medium => "medium",
        VideoTrack.Hard => "hard",
        _ => string.Empty
    };

    public static VideoTrack ToVideoTrack(string? key) => (key ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "easy" => VideoTrack.Easy,
        "medium" => VideoTrack.Medium,
        "hard" => VideoTrack.Hard,
        _ => VideoTrack.None
    };

    public static string Key(this VideoSubtitleStatus status) => status switch
    {
        VideoSubtitleStatus.Available => "available",
        VideoSubtitleStatus.Missing => "missing",
        _ => "unknown"
    };

    public static VideoSubtitleStatus ToSubtitleStatus(string? key) => (key ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "available" => VideoSubtitleStatus.Available,
        "missing" => VideoSubtitleStatus.Missing,
        _ => VideoSubtitleStatus.Unknown
    };

    public static VideoEmbedStatus ToEmbedStatus(string? key) => (key ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "playable" => VideoEmbedStatus.Playable,
        "blocked" => VideoEmbedStatus.Blocked,
        _ => VideoEmbedStatus.Unknown
    };

    public static string Key(this VideoLevelSource source) => source == VideoLevelSource.Subtitles ? "subtitles" : "estimate";

    public static VideoLevelSource ToLevelSource(string? key) =>
        string.Equals(key?.Trim(), "subtitles", StringComparison.OrdinalIgnoreCase) ? VideoLevelSource.Subtitles
            : VideoLevelSource.Estimate;

    public static CatalogVideoDto ToDto(this CatalogVideoRow x) => new(x.VideoId, x.Title, x.Duration, x.Level,
        x.ChannelTitle, x.Thumbnail, x.SubtitleStatus.Key(), x.Difficulty, x.Cefr, x.Track.Key(), x.LevelSource.Key());

    public static CatalogPathVideoDto ToPathDto(this CatalogVideoRow x) => new(x.VideoId, x.Title, x.Duration, x.Level,
        x.ChannelTitle, x.Thumbnail, x.SubtitleStatus.Key(), x.Difficulty, x.Cefr, x.Track.Key(), x.LevelSource.Key(),
        x.TopicSlug, x.TopicTitle);

    public static LangDto ToDto(this VideoLanguage x) => new(x.Code, x.Name);

    public static WatchedVideoDto ToDto(this VideoWatch x, bool mine, int shareCount = 1) => new(x.Id, x.VideoId,
        string.IsNullOrEmpty(x.Title) ? x.VideoId : x.Title, x.Duration, x.Language, x.IsPublic, x.OpenCount, mine,
        string.IsNullOrWhiteSpace(x.SharerName) ? DefaultSharerName : x.SharerName!, x.SharerAvatar ?? string.Empty,
        shareCount, VideoLinks.ThumbnailUrl(x.VideoId), Iso(x.LastOpenedAt));

    public static WatchProgressDto ToProgressDto(this VideoWatch x) => new(x.VideoId, x.Title, x.Duration,
        x.LastPosition, x.WatchedPercent, x.CompletedAt is not null, Iso(x.ProgressAt));

    public static SaveProgressResultDto ToSaveProgressDto(this VideoWatch x) =>
        new(x.VideoId, x.LastPosition, x.WatchedPercent, x.CompletedAt is not null);

    public static VideoSavedWordDto ToVideoDto(this SavedWord x) => new(x.Id, x.WordId, x.Word?.Headword ?? string.Empty,
        x.Word?.NormalizedHeadword ?? string.Empty, x.FolderId, x.Note, x.Word?.MeaningVi ?? string.Empty,
        x.Word?.Phonetic ?? string.Empty, x.Word?.PartOfSpeech.ToString() ?? string.Empty, x.Word?.IsCustom ?? false, x.SavedAt);

    public static VideoLearnerStateDto ToDto(this VideoLearnerState? x)
    {
        if (x is null) return new VideoLearnerStateDto(null, string.Empty, string.Empty, string.Empty);
        JsonElement? settings = null;
        if (!string.IsNullOrWhiteSpace(x.ReaderSettings))
        {
            try { settings = JsonDocument.Parse(x.ReaderSettings).RootElement.Clone(); }
            catch (JsonException) { settings = null; }
        }
        return new VideoLearnerStateDto(settings, x.ListenMode, x.PathTrack.Key(), Iso(x.UpdatedAt));
    }

    /// <summary>Gloss of a word; <c>partOfSpeech</c> = lowercase English part of speech ("" when unknown).</summary>
    public static GlossEntryDto ToGlossEntry(this WordGloss g, Guid? officialWordId)
    {
        var source = g.Sources.TryGetValue(GlossField.MeaningVi, out var meaning) ? meaning.Key()
            : g.Sources.Count > 0 ? g.Sources.Values.First().Key() : "none";
        return new GlossEntryDto(g.MeaningVi ?? string.Empty, g.Phonetic ?? string.Empty, PosKey(g.PartOfSpeech),
            g.Example ?? string.Empty, g.ExampleVi ?? string.Empty, g.DefinitionEn ?? string.Empty, source, officialWordId);
    }

    public static string PosKey(PartOfSpeech? pos) =>
        pos is null or PartOfSpeech.Other ? string.Empty : pos.Value.ToString().ToLowerInvariant();

    public static string Iso(DateTime? value) =>
        value is null ? string.Empty : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc).ToString("O");
}
