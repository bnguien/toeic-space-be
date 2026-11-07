using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>The imported word-level source: TOEIC list word → CEFR ("" = no CEFR) and the basic A1–A2 word set.</summary>
public sealed class WordLevelTable(IReadOnlyDictionary<string, string> levels, IReadOnlySet<string> basic)
{
    public static readonly WordLevelTable Empty = new(new Dictionary<string, string>(), new HashSet<string>());

    /// <summary>Words of the TOEIC list (keys) with their CEFR (may be empty).</summary>
    public IReadOnlyDictionary<string, string> Levels { get; } = levels;
    public IReadOnlySet<string> Basic { get; } = basic;
}

/// <summary>
/// Studychill <c>dictionary.vocab_levels</c>: CEFR of the transcript words found in the TOEIC list, through a few crude
/// base forms (planned → plan, studies → study). A1/A2 entries are left out (too basic to colour); an entry without
/// CEFR reads "TOEIC".
/// </summary>
public static partial class WordLevelRules
{
    public const int MaxWords = 5000;
    private static readonly (string Suffix, string[] Replacements)[] Suffixes =
    [
        ("ies", ["y"]), ("ied", ["y"]), ("ing", ["", "e"]), ("ed", ["", "e"]), ("es", ["", "e"]), ("s", [""])
    ];

    /// <summary>Word key as shown on the transcript: edge non-letters stripped, lowercase.</summary>
    public static string NormalizeWord(string word) => EdgeNonWord().Replace(word ?? string.Empty, string.Empty).ToLowerInvariant();

    public static IReadOnlyList<string> BaseForms(string word)
    {
        var candidates = new List<string> { word };
        var current = word;
        if (current.EndsWith("'s", StringComparison.Ordinal))
        {
            current = current[..^2];
            candidates.Add(current);
        }
        foreach (var (suffix, replacements) in Suffixes)
        {
            if (current.Length <= suffix.Length + 2 || !current.EndsWith(suffix, StringComparison.Ordinal)) continue;
            var root = current[..^suffix.Length];
            candidates.AddRange(replacements.Select(r => root + r));
            if (root.Length > 2 && root[^1] == root[^2]) candidates.Add(root[..^1]);
            break;
        }
        return candidates;
    }

    /// <summary>{word: "B1"|"B2"|"C1"|"C2"|"TOEIC"} for the given words (first <see cref="MaxWords"/>).</summary>
    public static Dictionary<string, string> Levels(IEnumerable<string> words, WordLevelTable table)
    {
        var result = new Dictionary<string, string>();
        foreach (var raw in words.Take(MaxWords))
        {
            var key = NormalizeWord(raw);
            if (key.Length < 3 || result.ContainsKey(key) || !key.All(char.IsAscii)) continue;
            foreach (var form in BaseForms(key))
            {
                if (!table.Levels.TryGetValue(form, out var cefr)) continue;
                var level = string.IsNullOrWhiteSpace(cefr) ? "TOEIC" : cefr.Trim().ToUpperInvariant();
                if (level is not ("A1" or "A2")) result[key] = level;
                break;
            }
        }
        return result;
    }

    /// <summary>Levels of every distinct lowercase whitespace token of the cues (sorted ordinally, like Studychill).</summary>
    public static Dictionary<string, string> LevelsOfText(IEnumerable<string> lines, WordLevelTable table)
    {
        var tokens = new SortedSet<string>(StringComparer.Ordinal);
        foreach (var line in lines)
            foreach (var token in line.ToLowerInvariant().Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
                tokens.Add(token);
        return Levels(tokens, table);
    }

    [GeneratedRegex("^[^0-9A-Za-zÀ-ɏ']+|[^0-9A-Za-zÀ-ɏ']+$")]
    private static partial Regex EdgeNonWord();
}
