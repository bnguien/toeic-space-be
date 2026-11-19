namespace ToeicSpace.Content.Application.Models.Vocabulary;

/// <summary>An active set in roadmap order (stage order, then set order) with its word count.</summary>
public sealed record SetOverview(Guid StageId, int StageOrder, Guid SetId, int SetOrder, string Title, int WordCount);
