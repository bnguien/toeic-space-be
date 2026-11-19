using System.IO.Compression;
using System.Text.Json;
using System.Text.RegularExpressions;
using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Models.NewsImport;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Services;

/// <summary>
/// Reads and validates the news import files (port of Studychill <c>import_article</c>): licences must be known,
/// covers credited to news agencies are dropped, empty sentences are skipped and sentence ids renumbered
/// (<c>p&lt;i&gt;s&lt;k&gt;</c>), word count and minutes recomputed, and an article is only published when every
/// sentence is translated and it has a Vietnamese title and a usable cover.
/// </summary>
public static partial class NewsImportParser
{
    public const int SchemaVersion = 1;
    public const long MaxDocumentBytes = 30 * 1024 * 1024;
    public const long MaxCoverBytes = 2 * 1024 * 1024;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static bool IsCover(NewsImportFile file) =>
        file.FileName.EndsWith(".webp", StringComparison.OrdinalIgnoreCase);

    public static bool IsDocument(NewsImportFile file) =>
        file.FileName.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        || file.FileName.EndsWith(".json.gz", StringComparison.OrdinalIgnoreCase);

    public static bool IsCoverName(string fileName) => CoverName().IsMatch(fileName);

    /// <summary>RIFF....WEBP header.</summary>
    public static bool IsWebp(byte[] content) =>
        content.Length > 12 && content[0] == 'R' && content[1] == 'I' && content[2] == 'F' && content[3] == 'F'
        && content[8] == 'W' && content[9] == 'E' && content[10] == 'B' && content[11] == 'P';

    /// <summary>Articles of one document (.json, or gzip-compressed .json.gz).</summary>
    public static IReadOnlyList<NewsArticleFileItem> ParseDocument(NewsImportFile file)
    {
        NewsArticlesDocument? document;
        try
        {
            using var stream = OpenText(file.Content);
            document = JsonSerializer.Deserialize<NewsArticlesDocument>(stream, Json);
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException or NotSupportedException)
        {
            throw NewsErrors.InvalidImportFile(file.FileName, "the file is not valid JSON.");
        }
        if (document is null || document.SchemaVersion != SchemaVersion)
            throw NewsErrors.InvalidImportFile(file.FileName, $"schemaVersion must be {SchemaVersion}.");
        if (document.Articles is not { Count: > 0 })
            throw NewsErrors.InvalidImportFile(file.FileName, "the file has no articles.");
        return document.Articles;
    }

    /// <summary>Validated entity values of one article; throws <c>invalid_import_file</c> with the reason.</summary>
    public static NewsArticle Normalize(NewsArticleFileItem item, string fileName)
    {
        var slug = Clip(item.Slug, 120);
        ContentRuleException Fail(string reason) => NewsErrors.InvalidImportFile(fileName,
            $"article '{(slug.Length > 0 ? slug : "?")}': {reason}");

        if (!SlugPattern().IsMatch(slug)) throw Fail("slug must be lower-case letters, digits and hyphens.");
        var source = item.Source ?? new NewsArticleFileSource();
        var sourceUrl = Clip(source.Url, 400);
        if (!sourceUrl.StartsWith("https://", StringComparison.Ordinal)) throw Fail("source.url must be an https link.");
        if (!NewsCatalog.IsLicense(source.License)) throw Fail($"unknown licence '{source.License}'.");
        if (!NewsCatalog.IsStage(item.Stage) || !NewsCatalog.IsTopic(item.Topic))
            throw Fail($"unknown stage or topic '{item.Stage}' / '{item.Topic}'.");
        var titleEn = Clip(item.TitleEn, 240);
        if (titleEn.Length == 0) throw Fail("titleEn is required.");

        var paragraphs = new List<NewsParagraph>();
        var translated = true;
        var wordCount = 0;
        foreach (var (paragraph, i) in (item.Paragraphs ?? []).Select((x, i) => (x, i + 1)))
        {
            var sentences = new List<NewsSentence>();
            foreach (var (sentence, k) in (paragraph?.Sentences ?? []).Select((x, k) => (x, k + 1)))
            {
                var text = Clip(sentence?.Text, 1200);
                if (text.Length == 0) continue;
                var translation = Clip(sentence?.Translation, 1200);
                translated &= translation.Length > 0;
                wordCount += NewsCatalog.CountWords(text);
                sentences.Add(new NewsSentence { Id = $"p{i}s{k}", Text = text, Translation = translation });
            }
            if (sentences.Count > 0) paragraphs.Add(new NewsParagraph { Id = $"p{i}", Sentences = sentences });
        }
        if (paragraphs.Count == 0) throw Fail("the article has no sentences.");

        var cover = item.Cover;
        var coverOk = cover is not null && IsCoverName(cover.File ?? string.Empty)
            && NewsCatalog.IsLicense(cover.License) && !NewsAgency().IsMatch($"{cover.Credit} {cover.Alt}");
        var titleVi = Optional(item.TitleVi, 240);
        if (!TryParseStatus(item.Status, out var status)) throw Fail($"unknown status '{item.Status}'.");
        if (status == NewsArticleStatus.Published && !(translated && titleVi is not null && coverOk))
            throw Fail("cannot be published without a translation of every sentence, a Vietnamese title and a usable cover.");
        if (status == NewsArticleStatus.Draft && translated && titleVi is not null) status = NewsArticleStatus.Review;

        return new NewsArticle
        {
            Slug = slug,
            SourceName = Clip(source.Name, 80),
            SourceUrl = sourceUrl,
            SourceId = Optional(source.Id, 40),
            SourceSeries = Optional(source.Series, 80),
            SourceLevel = Optional(source.Level, 40),
            Author = Optional(source.Author, 160),
            PublishedAt = source.PublishedAt?.UtcDateTime,
            License = source.License!,
            Attribution = Optional(source.Attribution, 300) ?? Clip($"Nguồn: {source.Name} ({sourceUrl})", 300),
            TitleEn = titleEn,
            TitleVi = titleVi,
            SummaryVi = Optional(item.SummaryVi, 400),
            Paragraphs = paragraphs,
            Vocabulary = NormalizeVocabulary(item.Vocabulary),
            WordCount = wordCount,
            Minutes = NewsCatalog.MinutesFor(wordCount),
            Stage = item.Stage!,
            Topic = item.Topic!,
            CoverFile = coverOk ? cover!.File : null,
            CoverAlt = coverOk ? Optional(cover!.Alt, 200) : null,
            CoverCredit = coverOk ? Optional(cover!.Credit, 300) : null,
            CoverLicense = coverOk ? cover!.License : null,
            CoverSourceUrl = coverOk ? Optional(cover!.SourceUrl, 500) : null,
            AudioUrl = Optional(item.AudioUrl, 500),
            Status = status,
            Featured = item.Featured
        };
    }

    /// <summary>Whitespace collapsed and trimmed, cut to <paramref name="max"/> characters.</summary>
    public static string Clip(string? value, int max)
    {
        var text = string.Join(' ',
            (value ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return text.Length > max ? text[..max] : text;
    }

    private static List<NewsVocabularyItem> NormalizeVocabulary(List<NewsArticleFileVocabulary>? items)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<NewsVocabularyItem>();
        foreach (var item in items ?? [])
        {
            var word = Clip(item?.Word, 60);
            if (word.Length == 0 || !seen.Add(word)) continue;
            result.Add(new NewsVocabularyItem
            {
                Word = word,
                Ipa = Clip(item!.Ipa, 80),
                PartOfSpeech = Clip(item.PartOfSpeech, 20),
                Meaning = Clip(item.Meaning, 120),
                Level = item.Level is { } level && NewsCatalog.VocabularyLevels.Contains(level)
                    ? level
                    : NewsCatalog.DefaultVocabularyLevel
            });
        }
        return result.Take(NewsCatalog.MaxVocabularyItems).ToList();
    }

    private static bool TryParseStatus(string? value, out NewsArticleStatus status)
    {
        status = NewsArticleStatus.Draft;
        if (string.IsNullOrWhiteSpace(value)) return true;
        return Enum.TryParse(value, ignoreCase: true, out status) && Enum.IsDefined(status)
            && !int.TryParse(value, out _);
    }

    private static Stream OpenText(byte[] content)
    {
        var raw = new MemoryStream(content, writable: false);
        if (content.Length < 2 || content[0] != 0x1f || content[1] != 0x8b) return raw;
        var unpacked = new MemoryStream();
        using (var gzip = new GZipStream(raw, CompressionMode.Decompress))
        {
            var buffer = new byte[81920];
            int read;
            while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
            {
                unpacked.Write(buffer, 0, read);
                if (unpacked.Length > MaxDocumentBytes * 2)
                    throw new InvalidDataException("The decompressed document is too large.");
            }
        }
        unpacked.Position = 0;
        return unpacked;
    }

    private static string? Optional(string? value, int max)
    {
        var text = Clip(value, max);
        return text.Length == 0 ? null : text;
    }

    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^[0-9a-f]{16,64}\\.webp$")]
    private static partial Regex CoverName();

    /// <summary>News agency photos are copyrighted and never republished (VOA terms), even when credited openly.</summary>
    [GeneratedRegex("\\b(AP|Associated Press|Reuters|REUTERS|AFP|Getty|EPA|Xinhua|Kyodo|Yonhap)\\b")]
    private static partial Regex NewsAgency();
}
