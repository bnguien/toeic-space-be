using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Domain.Rules.Videos;

/// <summary>Server grading of the reader's "Điền từ" (cloze) and "Viết lại" (rewrite) exercises. No XP.</summary>
public static partial class VideoExerciseGrader
{
    public const int MaxAnswersPerCheck = 12;
    public const int MaxWordsPerSentence = 120;
    public const int MinBlankLength = 3;

    /// <summary>
    /// Grades the blanks of one cleaned sentence: token index → typed text. Indexes out of range, sound tokens and
    /// tokens that cannot be blanks (shorter than 3 letters, not a plain word) are skipped.
    /// </summary>
    public static IReadOnlyDictionary<string, bool> GradeCloze(string sentence,
        IReadOnlyDictionary<string, string?> answers)
    {
        var tokens = sentence.Split(' ');
        var sound = TranscriptText.SoundMarks(tokens);
        var results = new Dictionary<string, bool>();
        foreach (var (rawIndex, guess) in answers)
        {
            if (!int.TryParse(rawIndex, out var j) || j < 0 || j >= tokens.Length || sound[j]) continue;
            var word = TranscriptText.CleanToken(tokens[j]);
            if (word.Length < MinBlankLength || !WordOnly().IsMatch(word)) continue;
            results[j.ToString()] = TranscriptText.Key(guess) == TranscriptText.Key(word);
        }
        return results;
    }

    /// <summary>Rewrite: typed words equal the spoken words of the sentence (sound tokens dropped, apostrophes ignored).</summary>
    public static bool GradeRewrite(string sentence, IEnumerable<string?> words)
    {
        var tokens = sentence.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
        var sound = TranscriptText.SoundMarks(tokens);
        var target = tokens.Where((_, i) => !sound[i]).Select(RewriteKey).Where(k => k.Length > 0).ToList();
        var typed = words.Select(w => RewriteKey(w ?? string.Empty)).Where(k => k.Length > 0).ToList();
        return target.Count > 0 && typed.SequenceEqual(target);
    }

    private static string RewriteKey(string token) => TranscriptText.Key(token).Replace("'", string.Empty);

    [GeneratedRegex("^[A-Za-z][A-Za-z-]*[A-Za-z]$")]
    private static partial Regex WordOnly();
}
