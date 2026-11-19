namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// The latest graded answer of a learner to one source question.
/// </summary>
public sealed record GradedAnswer(Guid SourceQuestionId, bool IsCorrect, DateTime GradedAt);
