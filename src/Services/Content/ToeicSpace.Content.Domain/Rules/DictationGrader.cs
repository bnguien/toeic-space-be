using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Rules;

/// <summary>Frame of one answer word: punctuation before, number of letter slots, punctuation after.</summary>
public sealed record SkeletonWord(string Before, int Length, string After);

public sealed record GradedWord(string Text, DictationWordStatus Status, string? Typed = null);

public sealed record DictationGrade(double Accuracy, bool Passed, int CorrectCount, int TotalCount,
    IReadOnlyList<GradedWord> Words);

/// <summary>
/// Word-level grading of "listen and write the sentence" (port of Studychill <c>listening.services.grading</c>):
/// the typed words are aligned to the answer with Python difflib's SequenceMatcher (no junk heuristics), so each
/// answer word is correct / wrong / missing and words the learner added are extra.
/// <para>One token list is used everywhere — skeleton, word proofs, hints and grading: whitespace tokens whose
/// core (letters, digits, apostrophes, hyphens inside the word) is not empty. Studychill kept zero-length tokens
/// on the server but dropped them in the client, so hint and proof indexes drifted after a "–" token.</para>
/// <para>A sentence passes only when every word is correct (Studychill passed at 90 % on the server while the
/// client required every word).</para>
/// </summary>
public static partial class DictationGrader
{
    /// <summary>Longest learner text accepted for grading (a long answer is a few hundred characters).</summary>
    public const int MaxUserText = 2000;

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    [GeneratedRegex(@"^[^0-9a-zA-ZÀ-ɏ']+")]
    private static partial Regex Lead();

    [GeneratedRegex(@"[^0-9a-zA-ZÀ-ɏ']+$")]
    private static partial Regex Tail();

    /// <summary>Trims, then cuts at <see cref="MaxUserText"/>.</summary>
    public static string CleanUserText(string? value)
    {
        var text = value?.Trim() ?? string.Empty;
        return text.Length > MaxUserText ? text[..MaxUserText] : text;
    }

    /// <summary>Comparison form of a token: punctuation stripped at both ends, lower case, no diacritics.</summary>
    public static string Normalize(string token)
    {
        var core = Tail().Replace(Lead().Replace(token, string.Empty), string.Empty).ToLowerInvariant();
        if (core.All(char.IsAscii)) return core;
        var decomposed = core.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark) builder.Append(ch);
        return builder.ToString().Normalize(NormalizationForm.FormC);
    }

    /// <summary>Whitespace tokens with a non-empty core, in sentence order.</summary>
    public static IReadOnlyList<string> Tokens(string? text) =>
        Whitespace().Split(text?.Trim() ?? string.Empty).Where(t => t.Length > 0 && Normalize(t).Length > 0).ToArray();

    public static IReadOnlyList<SkeletonWord> Skeleton(string text) =>
        Tokens(text).Select(raw =>
        {
            var before = Lead().Match(raw) is { Success: true } lead ? lead.Value : string.Empty;
            var rest = raw[before.Length..];
            var after = Tail().Match(rest) is { Success: true } tail ? tail.Value : string.Empty;
            return new SkeletonWord(before, rest.Length - after.Length, after);
        }).ToArray();

    public static DictationGrade Grade(string? userText, string targetText)
    {
        var userRaw = Tokens(userText);
        var targetRaw = Tokens(targetText);
        var user = userRaw.Select(Normalize).ToArray();
        var target = targetRaw.Select(Normalize).ToArray();
        var words = new List<GradedWord>(targetRaw.Count);
        var correct = 0;
        foreach (var (tag, i1, i2, j1, j2) in SequenceMatcher.Opcodes(target, user))
        {
            switch (tag)
            {
                case OpTag.Equal:
                    for (var k = i1; k < i2; k++) words.Add(new GradedWord(targetRaw[k], DictationWordStatus.Correct));
                    correct += i2 - i1;
                    break;
                case OpTag.Replace:
                    var span = j2 - j1;
                    for (var k = i1; k < i2; k++)
                    {
                        var index = k - i1;
                        words.Add(new GradedWord(targetRaw[k], DictationWordStatus.Wrong,
                            index < span ? userRaw[j1 + index] : string.Empty));
                    }
                    break;
                case OpTag.Delete:
                    for (var k = i1; k < i2; k++) words.Add(new GradedWord(targetRaw[k], DictationWordStatus.Missing));
                    break;
                case OpTag.Insert:
                    for (var k = j1; k < j2; k++) words.Add(new GradedWord(userRaw[k], DictationWordStatus.Extra));
                    break;
            }
        }
        var total = target.Length;
        var accuracy = total == 0 ? 0 : Math.Round(correct * 100.0 / total, 1, MidpointRounding.ToEven);
        return new DictationGrade(accuracy, total > 0 && correct == total, correct, total, words);
    }
}

public enum OpTag { Equal, Replace, Delete, Insert }

/// <summary>Python <c>difflib.SequenceMatcher(a, b, autojunk=False)</c> opcodes for string sequences.</summary>
public static class SequenceMatcher
{
    public static IReadOnlyList<(OpTag Tag, int I1, int I2, int J1, int J2)> Opcodes(IReadOnlyList<string> a,
        IReadOnlyList<string> b)
    {
        var b2j = new Dictionary<string, List<int>>(StringComparer.Ordinal);
        for (var j = 0; j < b.Count; j++)
        {
            if (!b2j.TryGetValue(b[j], out var list)) b2j[b[j]] = list = [];
            list.Add(j);
        }

        var blocks = new List<(int I, int J, int K)>();
        var queue = new Stack<(int, int, int, int)>();
        queue.Push((0, a.Count, 0, b.Count));
        while (queue.Count > 0)
        {
            var (alo, ahi, blo, bhi) = queue.Pop();
            var (i, j, k) = LongestMatch(a, b2j, alo, ahi, blo, bhi);
            if (k == 0) continue;
            blocks.Add((i, j, k));
            if (alo < i && blo < j) queue.Push((alo, i, blo, j));
            if (i + k < ahi && j + k < bhi) queue.Push((i + k, ahi, j + k, bhi));
        }
        blocks.Sort();

        var merged = new List<(int I, int J, int K)>();
        int i1 = 0, j1 = 0, k1 = 0;
        foreach (var (i2, j2, k2) in blocks)
        {
            if (i1 + k1 == i2 && j1 + k1 == j2) k1 += k2;
            else
            {
                if (k1 > 0) merged.Add((i1, j1, k1));
                (i1, j1, k1) = (i2, j2, k2);
            }
        }
        if (k1 > 0) merged.Add((i1, j1, k1));
        merged.Add((a.Count, b.Count, 0));

        var opcodes = new List<(OpTag, int, int, int, int)>();
        int ia = 0, jb = 0;
        foreach (var (ai, bj, size) in merged)
        {
            if (ia < ai && jb < bj) opcodes.Add((OpTag.Replace, ia, ai, jb, bj));
            else if (ia < ai) opcodes.Add((OpTag.Delete, ia, ai, jb, bj));
            else if (jb < bj) opcodes.Add((OpTag.Insert, ia, ai, jb, bj));
            ia = ai + size;
            jb = bj + size;
            if (size > 0) opcodes.Add((OpTag.Equal, ai, ia, bj, jb));
        }
        return opcodes;
    }

    private static (int, int, int) LongestMatch(IReadOnlyList<string> a, Dictionary<string, List<int>> b2j,
        int alo, int ahi, int blo, int bhi)
    {
        int besti = alo, bestj = blo, bestsize = 0;
        var j2len = new Dictionary<int, int>();
        for (var i = alo; i < ahi; i++)
        {
            var next = new Dictionary<int, int>();
            if (b2j.TryGetValue(a[i], out var indexes))
            {
                foreach (var j in indexes)
                {
                    if (j < blo) continue;
                    if (j >= bhi) break;
                    var k = next[j] = (j2len.TryGetValue(j - 1, out var previous) ? previous : 0) + 1;
                    if (k > bestsize) (besti, bestj, bestsize) = (i - k + 1, j - k + 1, k);
                }
            }
            j2len = next;
        }
        return (besti, bestj, bestsize);
    }
}
