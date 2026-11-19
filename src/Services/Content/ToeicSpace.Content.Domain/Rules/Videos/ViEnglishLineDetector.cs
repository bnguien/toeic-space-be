using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ToeicSpace.Content.Domain.ValueObjects;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>
/// YouTube's auto-translated Vietnamese track sometimes keeps a line in English. A Vietnamese line is "still English"
/// when it is ≥ 80% similar to the English line at the same start (or the same position when both tracks have the
/// same length) and carries no Vietnamese diacritics. One-word lines (names, numbers, "OK") are kept.
/// </summary>
public static partial class ViEnglishLineDetector
{
    public const double DuplicateThreshold = 0.8;

    public static IReadOnlyList<int> Find(IReadOnlyList<SubtitleCue> vi, IReadOnlyList<SubtitleCue> en)
    {
        var byStart = new Dictionary<double, SubtitleCue>();
        foreach (var cue in en) byStart[cue.Start] = cue;
        var sameLength = vi.Count == en.Count;
        var result = new List<int>();
        for (var i = 0; i < vi.Count; i++)
        {
            var source = MatchingSource(vi, en, byStart, sameLength, i);
            if (source is null) continue;
            var a = NormalizeLine(vi[i].Text);
            var b = NormalizeLine(source.Text);
            if (b.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2 || HasVietnameseMarks(a)) continue;
            if (a == b || SequenceRatio(a, b) >= DuplicateThreshold) result.Add(i);
        }
        return result;
    }

    /// <summary>The English cue a Vietnamese line translates: same start, else same position (equal lengths).</summary>
    public static SubtitleCue? MatchingSource(IReadOnlyList<SubtitleCue> vi, IReadOnlyList<SubtitleCue> en,
        IReadOnlyDictionary<double, SubtitleCue> byStart, bool sameLength, int index) =>
        byStart.TryGetValue(vi[index].Start, out var hit) ? hit
        : sameLength && index < en.Count ? en[index] : null;

    public static string NormalizeLine(string text) =>
        string.Join(' ', NonWord().Replace(text.ToLowerInvariant(), " ")
            .Split(' ', StringSplitOptions.RemoveEmptyEntries));

    public static bool HasVietnameseMarks(string text) => text.Contains('đ')
        || text.Normalize(NormalizationForm.FormD).Any(ch =>
            CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark);

    /// <summary>Python <c>difflib.SequenceMatcher(None, a, b).ratio()</c> (no autojunk: lines are short).</summary>
    public static double SequenceRatio(string a, string b)
    {
        var total = a.Length + b.Length;
        if (total == 0) return 1.0;
        return 2.0 * MatchingCharacters(a, 0, a.Length, b, 0, b.Length) / total;
    }

    private static int MatchingCharacters(string a, int alo, int ahi, string b, int blo, int bhi)
    {
        var (i, j, size) = LongestMatch(a, alo, ahi, b, blo, bhi);
        if (size == 0) return 0;
        return size + MatchingCharacters(a, alo, i, b, blo, j) + MatchingCharacters(a, i + size, ahi, b, j + size, bhi);
    }

    // difflib.find_longest_match: longest block, earliest in a, then earliest in b.
    private static (int I, int J, int Size) LongestMatch(string a, int alo, int ahi, string b, int blo, int bhi)
    {
        int bestI = alo, bestJ = blo, bestSize = 0;
        var lengths = new Dictionary<int, int>();
        for (var i = alo; i < ahi; i++)
        {
            var next = new Dictionary<int, int>();
            for (var j = blo; j < bhi; j++)
            {
                if (a[i] != b[j]) continue;
                var k = (lengths.TryGetValue(j - 1, out var previous) ? previous : 0) + 1;
                next[j] = k;
                if (k > bestSize)
                {
                    bestI = i - k + 1;
                    bestJ = j - k + 1;
                    bestSize = k;
                }
            }
            lengths = next;
        }
        return (bestI, bestJ, bestSize);
    }

    [GeneratedRegex(@"[^\w\s]+")]
    private static partial Regex NonWord();
}
