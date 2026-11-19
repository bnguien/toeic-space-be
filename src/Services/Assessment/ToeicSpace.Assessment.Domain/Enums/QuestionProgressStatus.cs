namespace ToeicSpace.Assessment.Domain.Enums;

/// <summary>
/// Learner progress on one source question, from the latest graded answer.
/// </summary>
public enum QuestionProgressStatus
{
    Unanswered = 0,
    Correct = 1,
    Wrong = 2
}
