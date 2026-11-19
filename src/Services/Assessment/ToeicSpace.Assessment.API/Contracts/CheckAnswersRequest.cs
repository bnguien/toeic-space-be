namespace ToeicSpace.Assessment.API.Contracts;

/// <param name="QuestionIds">Snapshot question IDs of the attempt (1–60).</param>
public sealed record CheckAnswersRequest(IReadOnlyList<Guid>? QuestionIds);
