namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// One snapshot question of an attempt with the learner's answer row, if any.
/// </summary>
public sealed record AttemptQuestionEntry(
    ToeicAttemptQuestion Row,
    QuestionSnapshot Question,
    ToeicAttemptAnswer? Answer)
{
    public bool IsChecked => Answer?.CheckedAt is not null;

    public bool IsAnswered => Answer?.UserAnswer is not null;

    /// <summary>Null when unanswered (skipped).</summary>
    public bool? IsCorrect => Answer?.UserAnswer is { } answer ? answer == Question.CorrectAnswer : null;
}
