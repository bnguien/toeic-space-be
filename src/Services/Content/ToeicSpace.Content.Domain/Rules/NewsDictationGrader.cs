using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Domain.Rules;

/// <summary>One word of the answer and whether the learner typed it in the right place.</summary>
public sealed record NewsDictationWordMark(string Text, bool Ok);

/// <summary>
/// Result of grading one dictation sentence: <see cref="Words"/> follows the words of the answer;
/// <see cref="Extra"/> counts typed words that match nothing.
/// </summary>
public sealed record NewsDictationGrade(bool Correct, IReadOnlyList<NewsDictationWordMark> Words, int CorrectCount, int Total,
    int Extra);

/// <summary>
/// Grades a typed sentence against the answer (port of Studychill <c>grade_dictation</c>): words are compared by a
/// key (lower case, curly apostrophes straightened, punctuation and hyphens dropped) and aligned with the longest
/// common subsequence, so wrong, missing and extra words are each marked in the right place.
/// </summary>
public static partial class NewsDictationGrader
{
    public static NewsDictationGrade Grade(string? typed, string? answer)
    {
        var answerWords = Split(answer).Where(x => MatchKey(x).Length > 0).ToArray();
        var answerKeys = answerWords.Select(MatchKey).ToArray();
        var typedKeys = Split(typed).Select(MatchKey).Where(x => x.Length > 0).ToArray();
        int n = typedKeys.Length, m = answerKeys.Length;

        var lcs = new int[n + 1, m + 1];
        for (var i = n - 1; i >= 0; i--)
            for (var j = m - 1; j >= 0; j--)
                lcs[i, j] = typedKeys[i] == answerKeys[j]
                    ? lcs[i + 1, j + 1] + 1
                    : Math.Max(lcs[i + 1, j], lcs[i, j + 1]);

        var ok = new bool[m];
        int a = 0, b = 0;
        while (a < n && b < m)
        {
            if (typedKeys[a] == answerKeys[b])
            {
                ok[b] = true;
                a++;
                b++;
            }
            else if (lcs[a + 1, b] >= lcs[a, b + 1]) a++;
            else b++;
        }

        var correctCount = ok.Count(x => x);
        var extra = n - correctCount;
        var words = answerWords.Select((word, index) => new NewsDictationWordMark(word, ok[index])).ToArray();
        return new NewsDictationGrade(m > 0 && correctCount == m && extra == 0, words, correctCount, m, extra);
    }

    /// <summary>Comparison key of a word: "Four-day," → "fourday", "It’s" → "it's", "'quoted'" → "quoted".</summary>
    public static string MatchKey(string word) =>
        NonKeyCharacters().Replace(word.ToLowerInvariant().Replace('’', '\'').Replace('‘', '\''), string.Empty)
            .Trim('\'');

    private static string[] Split(string? text) =>
        (text ?? string.Empty).Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

    [GeneratedRegex("[^a-z0-9']")]
    private static partial Regex NonKeyCharacters();
}
