namespace ToeicSpace.Assessment.API.Contracts;

/// <param name="TimeSpentSeconds">Seconds spent on the question since the last save (0–3600), added to its total.</param>
public sealed record SaveAnswerRequest(
    AnswerOption? Answer,
    long ExpectedRevision,
    Guid MutationId,
    int? TimeSpentSeconds);
