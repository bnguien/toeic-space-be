using ToeicSpace.Assessment.Domain.Common;
using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Domain.Entities;

public class ToeicAttemptAnswer : BaseAuditableEntity
{
    public Guid AttemptId { get; set; }

    public ToeicAttempt Attempt { get; set; } = null!;

    public Guid QuestionId { get; set; }

    public ToeicQuestion Question { get; set; } = null!;

    public AnswerOption? UserAnswer { get; set; }

    /// <summary>
    /// Snapshot of the answer key at submission time, so later edits never change past results.
    /// </summary>
    public AnswerOption CorrectAnswer { get; set; }

    public bool IsCorrect { get; set; }

    public int TimeSpentSeconds { get; set; }
}
