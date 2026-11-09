using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>
/// Token rules shared with the reader (<c>subtitleReading.js</c>): token cleaning, sound tokens, guess keys and the
/// cue cleaning that keeps the browser's sentence/token indexes aligned with the server's.
/// </summary>
public static partial class TranscriptText
{
    public const string Punctuation = ".,!?;:\"()[]{}<>…—–*_/|~`@#$%^&+=";
    private const string LeadingPunctuation = ".,;:!?-–—…>»·•";

    /// <summary>Strips punctuation at both ends, then edge apostrophes (inner ones stay: don't, it's).</summary>
    public static string CleanToken(string token)
    {
        int a = 0, b = token.Length;
        while (a < b && Punctuation.Contains(token[a])) a++;
        while (b > a && Punctuation.Contains(token[b - 1])) b--;
        return token[a..b].Trim('\'', '’');
    }

    /// <summary>Marks [Music], (laughs) and ♪ tokens, which are not speech.</summary>
    public static bool[] SoundMarks(IReadOnlyList<string> tokens)
    {
        var marks = new bool[tokens.Count];
        char? closing = null;
        for (var i = 0; i < tokens.Count; i++)
        {
            var token = tokens[i];
            if (closing is null && token.Length > 0 && token[0] is '[' or '(') closing = token[0] == '[' ? ']' : ')';
            if (closing is { } close)
            {
                marks[i] = true;
                if (token.Contains(close)) closing = null;
                continue;
            }
            if (SoundOnly().IsMatch(token)) marks[i] = true;
        }
        return marks;
    }

    /// <summary>Comparison key of a guess: case, punctuation, curly apostrophes and diacritics are ignored.</summary>
    public static string Key(string? value)
    {
        var decomposed = (value ?? string.Empty).Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        var text = builder.ToString().ToLowerInvariant()
            .Replace('’', '\'').Replace('‘', '\'').Replace('`', '\'');
        return NonKey().Replace(text, string.Empty);
    }

    /// <summary>
    /// Drops speaker marks ('&gt;&gt;'), stray leading punctuation and cues without a letter/digit — the same cleaning
    /// the reader applies, so a sentence index from the browser points at the same cue here.
    /// </summary>
    public static IReadOnlyList<SubtitleCue> CleanCues(IEnumerable<SubtitleCue> cues)
    {
        var result = new List<SubtitleCue>();
        foreach (var cue in cues)
        {
            var words = cue.Text.Split(' ').Where(t => t.Length > 0 && !SpeakerMark().IsMatch(t)).ToList();
            while (words.Count > 0)
            {
                var a = 0;
                while (a < words[0].Length && LeadingPunctuation.Contains(words[0][a])) a++;
                if (a == 0) break;
                if (a < words[0].Length)
                {
                    words[0] = words[0][a..];
                    break;
                }
                words.RemoveAt(0);
            }
            var text = string.Join(' ', words);
            if (text.Length > 0 && text.Any(char.IsLetterOrDigit)) result.Add(new SubtitleCue(cue.Start, cue.Duration, text));
        }
        return result;
    }

    [GeneratedRegex("^(?:♪|♫|\U0001F3B5)+$")]
    private static partial Regex SoundOnly();

    [GeneratedRegex("[^a-z0-9']")]
    private static partial Regex NonKey();

    [GeneratedRegex("^(?:>+|(?:&gt;)+)$")]
    private static partial Regex SpeakerMark();
}
