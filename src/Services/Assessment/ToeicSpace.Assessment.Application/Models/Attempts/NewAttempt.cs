namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// Everything needed to create an attempt with its immutable snapshot.
/// </summary>
/// <param name="Questions">Snapshot questions in attempt order.</param>
/// <param name="TimeLimitSeconds">Set for timed (FullTest) attempts; the deadline is start + limit.</param>
public sealed record NewAttempt(
    Guid UserId,
    Guid? TestId,
    Guid? PracticeSetId,
    AttemptMode Mode,
    string Title,
    AttemptSnapshotDocument Document,
    IReadOnlyList<QuestionSnapshot> Questions,
    int? TimeLimitSeconds,
    DateTime Now);
