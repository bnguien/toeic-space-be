using System.Text.Json;
using System.Text.RegularExpressions;
using ToeicSpace.Content.Application.Models.LibraryImport;
using ToeicSpace.Content.Application.Services.Enrichment;
using ToeicSpace.Content.Domain.Entities;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Services.Import;

/// <summary>
/// The single parser for learner word lists (the FE only renders its preview).
/// <para>Text: one entry per line; list markers are stripped; the first separator found in the order TAB, " => ",
/// " -> ", " = ", " : ", " - ", " – ", " — " splits headword and Vietnamese meaning; a line without one is split on
/// <c>, ; ， 、 / | +</c> into bare headwords. A trailing "(n)" / "[adj]" sets the part of speech.</para>
/// <para>JSON: <c>[{category, words:[…]}]</c>, <c>{categories:[…]}</c>, <c>{words|data|vocabulary:[…]}</c>,
/// <c>{word: meaning}</c>, plain arrays of strings or word objects, with Vietnamese/English field aliases;
/// ```json fences are unwrapped.</para>
/// Every headword needs a latin letter and at most 5 words; repeated headwords are marked <c>Duplicate</c>.
/// </summary>
public static partial class VocabularyTextParser
{
    public const int MaxTextLength = 20_000;
    public const int MaxCategoryLength = VocabularyFolder.NameMaxLength;

    private static readonly string[] Separators = ["\t", " => ", " -> ", " = ", " : ", " - ", " – ", " — "];
    private static readonly string[] WordKeys = ["word", "vocab", "term", "tu", "headword"];
    private static readonly string[] MeaningKeys = ["meaning", "vi", "nghia", "definition", "meaningVi", "meaning_vi"];
    private static readonly string[] PhoneticKeys = ["phonetic", "ipa", "phien_am"];
    private static readonly string[] PosKeys = ["pos", "type", "loai_tu", "partOfSpeech", "part_of_speech"];
    private static readonly string[] ExampleKeys = ["example", "ex", "vi_du", "sentence"];
    private static readonly string[] ExampleViKeys = ["example_vi", "ex_vi", "dich_vi_du", "sentence_vi", "exampleVi"];
    private static readonly string[] CategoryKeys = ["category", "topic", "group", "folder", "title", "name"];
    private static readonly string[] ListKeys = ["words", "data", "vocabulary", "items"];
    // Brackets are kept: a note that is not a part of speech, e.g. "(informal)", stays in the headword.
    private const string EdgePunctuation = ".,;:!?\"'“”‘’`*_<>«»";

    public static ParsedImport ParseText(string text)
    {
        var builder = new Builder();
        var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        for (var i = 0; i < lines.Length; i++)
        {
            var line = ListMarker().Replace(lines[i].Trim(), string.Empty).Trim();
            if (line.Length == 0) continue;
            var separator = Separators.FirstOrDefault(line.Contains);
            if (separator is not null)
            {
                var at = line.IndexOf(separator, StringComparison.Ordinal);
                var meaning = line[(at + separator.Length)..].Trim();
                var meaningPos = LeadingPos().Match(meaning);
                PartOfSpeech? pos = null;
                if (meaningPos.Success && PosNormalizer.Normalize(meaningPos.Groups[1].Value) is { } p and not PartOfSpeech.Other)
                {
                    pos = p;
                    meaning = meaning[meaningPos.Length..].Trim();
                }
                builder.Add(i + 1, lines[i].Trim(), line[..at], Clean(meaning), null, pos, null, null, null);
            }
            else
                foreach (var part in BareSeparators().Split(line).Where(x => x.Trim().Length > 0))
                    builder.Add(i + 1, part.Trim(), part, null, null, null, null, null, null);
        }
        return builder.Build();
    }

    public static ParsedImport ParseJson(string json)
    {
        var fenced = Fence().Match(json);
        var body = fenced.Success ? fenced.Groups[1].Value : json.Trim();
        JsonElement root;
        try
        {
            using var document = JsonDocument.Parse(body, new JsonDocumentOptions
            {
                AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip
            });
            root = document.RootElement.Clone();
        }
        catch (JsonException ex)
        {
            throw new ContentRuleException("invalid_import_json",
                $"The JSON could not be read (line {ex.LineNumber + 1}).");
        }

        var builder = new Builder();
        ReadContainer(root, null, builder);
        return builder.Build();
    }

    private static void ReadContainer(JsonElement element, string? category, Builder builder)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in element.EnumerateArray()) ReadEntry(entry, category, builder);
            return;
        }
        if (element.ValueKind != JsonValueKind.Object)
        {
            builder.Reject(element.ToString(), "invalid_item");
            return;
        }
        if (Property(element, ["categories"]) is { ValueKind: JsonValueKind.Array } categories)
        {
            ReadContainer(categories, category, builder);
            return;
        }
        if (Property(element, ListKeys) is { ValueKind: JsonValueKind.Array } list)
        {
            ReadContainer(list, Text(element, CategoryKeys) ?? category, builder);
            return;
        }
        if (Text(element, WordKeys) is not null)
        {
            ReadEntry(element, category, builder);
            return;
        }
        // { "word": "meaning" }, { "Topic": [ … ] } or { "word": { meaning … } }
        foreach (var property in element.EnumerateObject())
        {
            switch (property.Value.ValueKind)
            {
                case JsonValueKind.Array:
                    ReadContainer(property.Value, property.Name, builder);
                    break;
                case JsonValueKind.Object:
                    ReadItem(property.Value, property.Name, category, builder);
                    break;
                default:
                    builder.Add(builder.NextLine(), property.Name, property.Name, Clean(ValueText(property.Value)),
                        null, null, null, null, category);
                    break;
            }
        }
    }

    private static void ReadEntry(JsonElement entry, string? category, Builder builder)
    {
        switch (entry.ValueKind)
        {
            case JsonValueKind.String:
                builder.Add(builder.NextLine(), entry.GetString()!, entry.GetString()!, null, null, null, null, null, category);
                break;
            case JsonValueKind.Object when Property(entry, ListKeys) is { ValueKind: JsonValueKind.Array } words:
                ReadContainer(words, Text(entry, CategoryKeys) ?? category, builder);
                break;
            case JsonValueKind.Object when Text(entry, WordKeys) is not null:
                ReadItem(entry, null, category, builder);
                break;
            case JsonValueKind.Object:
                ReadContainer(entry, category, builder);
                break;
            default:
                builder.Reject(entry.ToString(), "invalid_item");
                break;
        }
    }

    private static void ReadItem(JsonElement item, string? headword, string? category, Builder builder)
    {
        var word = Text(item, WordKeys) ?? headword ?? string.Empty;
        var ownCategory = Text(item, ["category", "topic", "group", "folder"]);
        builder.Add(builder.NextLine(), word, word, Clean(Text(item, MeaningKeys)), Clean(Text(item, PhoneticKeys)),
            PosNormalizer.Normalize(Text(item, PosKeys)), Clean(Text(item, ExampleKeys)),
            Clean(Text(item, ExampleViKeys)), ownCategory ?? category);
    }

    private static JsonElement? Property(JsonElement element, string[] names)
    {
        foreach (var property in element.EnumerateObject())
            if (names.Any(n => string.Equals(n, property.Name, StringComparison.OrdinalIgnoreCase)))
                return property.Value;
        return null;
    }

    private static string? Text(JsonElement element, string[] names) =>
        Property(element, names) is { } value ? ValueText(value) : null;

    private static string? ValueText(JsonElement value) => value.ValueKind switch
    {
        JsonValueKind.String => string.IsNullOrWhiteSpace(value.GetString()) ? null : value.GetString(),
        JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False => value.ToString(),
        _ => null
    };

    private static string? Clean(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : string.Join(' ', value.Split((char[]?)null,
            StringSplitOptions.RemoveEmptyEntries));

    private sealed class Builder
    {
        private readonly List<ParsedImportItem> _items = [];
        private readonly List<ParsedRejectedLine> _rejected = [];
        private readonly HashSet<string> _seen = [];
        private int _line;

        public int NextLine() => ++_line;

        public void Reject(string text, string code) => _rejected.Add(new ParsedRejectedLine(NextLine(), text, code));

        public void Add(int line, string source, string rawHeadword, string? meaning, string? phonetic,
            PartOfSpeech? pos, string? example, string? exampleVi, string? category)
        {
            _line = Math.Max(_line, line);
            var head = rawHeadword.Trim();
            var marker = TrailingPos().Match(head);
            if (marker.Success && PosNormalizer.Normalize(marker.Groups[1].Value) is { } p and not PartOfSpeech.Other)
            {
                pos ??= p;
                head = head[..marker.Index];
            }
            head = string.Join(' ', head.Trim().Trim(EdgePunctuation.ToCharArray()).Trim()
                .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));

            var input = InputNormalizer.Normalize(head);
            var code = !InputNormalizer.HasLatinLetter(head) ? "no_latin_letter"
                : head.Length > InputNormalizer.MaxLength ? "too_long"
                : input.TokenCount > InputNormalizer.MaxPhraseTokens ? "too_many_words"
                : null;
            if (code is not null)
            {
                _rejected.Add(new ParsedRejectedLine(line, source, code));
                return;
            }
            var cleanCategory = Clean(category);
            if (cleanCategory?.Length > MaxCategoryLength) cleanCategory = cleanCategory[..MaxCategoryLength].Trim();
            _items.Add(new ParsedImportItem(line, input.Text, meaning, phonetic, pos, example, exampleVi, cleanCategory,
                !_seen.Add(input.Key)));
        }

        public ParsedImport Build() => new(_items, _rejected.OrderBy(x => x.Line).ToArray());
    }

    [GeneratedRegex(@"^(\d+[./)\-:]\s*|[-*•+~▪▫✓✔>#@&§%]\s*)+")]
    private static partial Regex ListMarker();

    [GeneratedRegex(@"[,;，、/|+]")]
    private static partial Regex BareSeparators();

    [GeneratedRegex(@"\s*[\(\[]\s*([A-Za-z][A-Za-z. ]{0,19})\s*[\)\]]\s*$")]
    private static partial Regex TrailingPos();

    [GeneratedRegex(@"^[\(\[]\s*([A-Za-z][A-Za-z. ]{0,19})\s*[\)\]]\s*")]
    private static partial Regex LeadingPos();

    [GeneratedRegex(@"```(?:json|JSON)?\s*(.*?)```", RegexOptions.Singleline)]
    private static partial Regex Fence();
}
