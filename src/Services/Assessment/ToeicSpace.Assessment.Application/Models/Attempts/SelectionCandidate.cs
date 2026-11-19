namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// A question that can be put into a practice session; questions sharing a passage are selected together.
/// </summary>
public sealed record SelectionCandidate(Guid QuestionId, Guid? PassageId);
