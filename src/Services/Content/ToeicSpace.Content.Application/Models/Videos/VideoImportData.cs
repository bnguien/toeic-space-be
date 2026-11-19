using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Models.Videos;

/// <summary>Content of one data file of <c>data/videos</c>; exactly one of the parts is set.</summary>
public sealed record VideoImportData(string FileName, ImportedCatalog? Catalog, ImportedSubtitles? Subtitles);

public sealed record ImportedTopic(string Slug, string Title, int Position);

public sealed record ImportedCatalogVideo(string VideoId, string Topic, string Title, string ChannelHandle,
    string ChannelTitle, int Duration, string Level, string Thumbnail, long ViewCount, int Position, bool IsActive,
    string SubtitleStatus, string EmbedStatus, string EmbedReason, int Difficulty, string Cefr, string Track,
    string LevelSource);

/// <summary><c>catalog.json</c>.</summary>
public sealed record ImportedCatalog(IReadOnlyList<ImportedTopic> Topics, IReadOnlyList<ImportedCatalogVideo> Videos);

public sealed record ImportedLanguageList(string VideoId, IReadOnlyList<VideoLanguage> Manual,
    IReadOnlyList<VideoLanguage> Auto, string Title, int Duration, string Language);

public sealed record ImportedTrack(string VideoId, string Lang, IReadOnlyList<SubtitleCue> Cues);

/// <summary><c>subtitles-NN.json.gz</c>.</summary>
public sealed record ImportedSubtitles(IReadOnlyList<ImportedLanguageList> Languages, IReadOnlyList<ImportedTrack> Tracks);
