using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Dictation;

public sealed record DictationHintDto(int Index, int Total, DictationHintMode Mode, int Length, string? Word,
    string? Letter);

public sealed record CheckWordDto(string Text, DictationWordStatus Status, string? Typed);

/// <summary>
/// Result of a submission. <paramref name="IsCorrect"/> is true only when every word is correct. A dry run that
/// is not correct hides the answer (<paramref name="CorrectText"/>, <paramref name="Translation"/> and the text of
/// wrong/missing words are empty).
/// </summary>
public sealed record DictationCheckDto(double Accuracy, bool IsCorrect, int CorrectCount, int TotalCount,
    IReadOnlyList<CheckWordDto> Words, string CorrectText, string Translation, bool DryRun, int Score,
    double BestAccuracy, bool Saved, bool Assisted, bool PassedClean, bool UnlocksNext, bool NextUnlocked);

public sealed record DictationSummaryDto(bool Authenticated, int PassedCount, int TotalCount, int Accuracy);
