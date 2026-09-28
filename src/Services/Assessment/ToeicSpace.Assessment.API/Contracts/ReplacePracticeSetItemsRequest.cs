namespace ToeicSpace.Assessment.API.Contracts;

public sealed record ReplacePracticeSetItemsRequest(IReadOnlyList<Guid> QuestionIds);
