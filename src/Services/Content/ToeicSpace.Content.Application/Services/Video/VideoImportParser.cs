using System.IO.Compression;
using System.Text.Json;
using ToeicSpace.Content.Application.Models.Videos;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules.Videos;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Application.Services.Video;

/// <summary>
/// Reads the files of <c>data/videos</c> (<c>catalog.json</c>, <c>subtitles-NN.json.gz</c>):
/// gzip is detected by its magic bytes, the kind by its top-level members, <c>schemaVersion</c> must be 1. Any invalid
/// row rejects the whole file (<c>invalid_import_file</c>).
/// </summary>
public static class VideoImportParser
{
    public const int SchemaVersion = 1;
    public const long MaxDecompressedBytes = 96L * 1024 * 1024;
    private static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true };

    public static VideoImportData Parse(VideoImportFile file)
    {
        var name = file.FileName;
        using var document = ReadDocument(file);
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object) throw Invalid(name, "the file must contain a JSON object.");
        if (!root.TryGetProperty("schemaVersion", out var version) || version.ValueKind != JsonValueKind.Number
            || version.GetInt32() != SchemaVersion)
            throw Invalid(name, $"schemaVersion must be {SchemaVersion}.");

        if (root.TryGetProperty("topics", out _) || root.TryGetProperty("videos", out _))
            return new VideoImportData(name, ParseCatalog(name, root), null);
        if (root.TryGetProperty("tracks", out _) || root.TryGetProperty("languages", out _))
            return new VideoImportData(name, null, ParseSubtitles(name, root));
        throw Invalid(name, "unknown file: expected topics/videos or tracks/languages.");
    }

    private static JsonDocument ReadDocument(VideoImportFile file)
    {
        try
        {
            if (file.Content.Length >= 2 && file.Content[0] == 0x1f && file.Content[1] == 0x8b)
            {
                using var gzip = new GZipStream(new MemoryStream(file.Content), CompressionMode.Decompress);
                using var buffer = new MemoryStream();
                var chunk = new byte[81920];
                int read;
                while ((read = gzip.Read(chunk, 0, chunk.Length)) > 0)
                {
                    buffer.Write(chunk, 0, read);
                    if (buffer.Length > MaxDecompressedBytes) throw Invalid(file.FileName, "decompressed file is too large.");
                }
                return JsonDocument.Parse(buffer.ToArray());
            }
            return JsonDocument.Parse(file.Content);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw Invalid(file.FileName, "unreadable JSON or gzip.");
        }
    }

    private static ImportedCatalog ParseCatalog(string name, JsonElement root)
    {
        var topics = new List<ImportedTopic>();
        foreach (var t in Array(root, "topics"))
        {
            var slug = Str(t, "slug");
            if (slug.Length is 0 or > VideoCatalogItem.TopicSlugMaxLength) throw Invalid(name, $"invalid topic slug '{slug}'.");
            topics.Add(new ImportedTopic(slug, Cut(Str(t, "title"), VideoCatalogItem.TopicTitleMaxLength), Int(t, "position")));
        }
        if (topics.Select(t => t.Slug).Distinct().Count() != topics.Count) throw Invalid(name, "duplicate topic slug.");

        var videos = new List<ImportedCatalogVideo>();
        foreach (var v in Array(root, "videos"))
        {
            var id = Str(v, "videoId");
            if (!VideoLinks.IsValidVideoId(id)) throw Invalid(name, $"invalid videoId '{id}'.");
            var topic = Str(v, "topic");
            if (topic.Length == 0 || topics.All(t => t.Slug != topic)) throw Invalid(name, $"video {id} has an unknown topic.");
            videos.Add(new ImportedCatalogVideo(id, topic, Cut(Str(v, "title"), VideoCatalogItem.TitleMaxLength),
                Cut(Str(v, "channelHandle"), VideoCatalogItem.ChannelHandleMaxLength),
                Cut(Str(v, "channelTitle"), VideoCatalogItem.ChannelTitleMaxLength), Math.Max(0, Int(v, "duration")),
                Cut(Str(v, "level"), 4), Cut(Str(v, "thumbnail"), VideoCatalogItem.ThumbnailMaxLength),
                Math.Max(0, Long(v, "viewCount")), Math.Max(0, Int(v, "position")), Bool(v, "isActive", true),
                Str(v, "subtitleStatus"), Str(v, "embedStatus"), Cut(Str(v, "embedReason"), VideoCatalogItem.EmbedReasonMaxLength),
                Math.Clamp(Int(v, "difficulty"), 0, 100), Cut(Str(v, "cefr"), 4), Str(v, "track"), Str(v, "levelSource")));
        }
        if (videos.Select(v => v.VideoId).Distinct().Count() != videos.Count) throw Invalid(name, "duplicate videoId.");
        return new ImportedCatalog(topics, videos);
    }

    private static ImportedSubtitles ParseSubtitles(string name, JsonElement root)
    {
        var languages = new List<ImportedLanguageList>();
        foreach (var l in Array(root, "languages"))
        {
            var id = Str(l, "videoId");
            if (!VideoLinks.IsValidVideoId(id)) throw Invalid(name, $"invalid videoId '{id}'.");
            languages.Add(new ImportedLanguageList(id, Languages(l, "manual"), Languages(l, "auto"),
                Cut(Str(l, "title"), VideoCatalogItem.TitleMaxLength), Math.Max(0, Int(l, "duration")),
                Cut(Str(l, "language"), VideoSubtitle.LanguageMaxLength)));
        }
        var tracks = new List<ImportedTrack>();
        foreach (var t in Array(root, "tracks"))
        {
            var id = Str(t, "videoId");
            var lang = Str(t, "lang");
            if (!VideoLinks.IsValidVideoId(id) || !VideoLinks.IsValidLang(lang))
                throw Invalid(name, $"invalid track key '{id}/{lang}'.");
            if (!t.TryGetProperty("cues", out var cuesElement) || cuesElement.ValueKind != JsonValueKind.Array)
                throw Invalid(name, $"track {id}/{lang} has no cues array.");
            List<SubtitleCue> cues;
            try { cues = cuesElement.Deserialize<List<SubtitleCue>>(Json) ?? []; }
            catch (JsonException) { throw Invalid(name, $"track {id}/{lang} has an invalid cue."); }
            tracks.Add(new ImportedTrack(id, lang, cues));
        }
        if (tracks.Select(t => (t.VideoId, t.Lang.ToLowerInvariant())).Distinct().Count() != tracks.Count)
            throw Invalid(name, "duplicate track.");
        return new ImportedSubtitles(languages, tracks);
    }

    /// <summary>
    /// Language list entries; codes that can never be requested (YouTube's named tracks such as
    /// <c>en-ehkg1hFWq8A</c>, longer than a language code) are skipped rather than rejecting the file.
    /// </summary>
    private static List<VideoLanguage> Languages(JsonElement parent, string property) =>
        Array(parent, property).Select(x => (Code: Str(x, "code"), Name: Str(x, "name")))
            .Where(x => VideoLinks.IsValidLang(x.Code))
            .Select(x => new VideoLanguage(x.Code, Cut(x.Name, 80))).ToList();

    private static IEnumerable<JsonElement> Array(JsonElement parent, string property) =>
        parent.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.EnumerateArray() : [];

    private static string Str(JsonElement e, string property) =>
        e.ValueKind == JsonValueKind.Object && e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString()!.Trim() : string.Empty;

    private static int Int(JsonElement e, string property) =>
        e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number
            ? (v.TryGetInt32(out var i) ? i : (int)Math.Clamp(v.GetDouble(), int.MinValue, int.MaxValue)) : 0;

    private static long Long(JsonElement e, string property) =>
        e.TryGetProperty(property, out var v) && v.ValueKind == JsonValueKind.Number
            ? (v.TryGetInt64(out var l) ? l : (long)v.GetDouble()) : 0;

    private static bool Bool(JsonElement e, string property, bool fallback) =>
        e.TryGetProperty(property, out var v) && v.ValueKind is JsonValueKind.True or JsonValueKind.False
            ? v.GetBoolean() : fallback;

    private static string Cut(string value, int max) => value.Length <= max ? value : value[..max];

    private static ContentRuleException Invalid(string fileName, string reason) => VideoErrors.InvalidImportFile(fileName, reason);
}
