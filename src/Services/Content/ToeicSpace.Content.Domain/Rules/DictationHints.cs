using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Rules;

public sealed record DictationHint(int Index, int Total, DictationHintMode Mode, int Length, string? Word,
    string? Letter);

/// <summary>
/// Reveals ONE word at the first wrong or missing position of what the learner typed, aligned with the same
/// algorithm as grading (Studychill <c>hint</c>). The index refers to <see cref="DictationGrader.Tokens"/>, the
/// list the skeleton exposes, so it always matches the client's word slots.
/// </summary>
public static class DictationHints
{
    private static readonly char[] Punctuation = ".,!?;:\"()[]{}…“”‘’'".ToCharArray();

    public static DictationHint? For(string? userText, string text, DictationHintMode mode)
    {
        var tokens = DictationGrader.Tokens(text);
        if (tokens.Count == 0) return null;
        var aligned = DictationGrader.Grade(userText, text).Words
            .Where(w => w.Status != DictationWordStatus.Extra).ToList();
        var index = aligned.FindIndex(w => w.Status != DictationWordStatus.Correct);
        index = Math.Clamp(index < 0 ? tokens.Count - 1 : index, 0, tokens.Count - 1);
        var raw = tokens[index];
        var core = raw.Trim(Punctuation);
        if (core.Length == 0) core = raw;
        return mode == DictationHintMode.RevealWord
            ? new DictationHint(index, tokens.Count, mode, core.Length, raw, null)
            : new DictationHint(index, tokens.Count, mode, core.Length, null, core[..1]);
    }
}
