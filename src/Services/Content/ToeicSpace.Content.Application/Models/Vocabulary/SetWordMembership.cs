namespace ToeicSpace.Content.Application.Models.Vocabulary;

/// <summary>One word of an active set in an active stage (read model for counts, progress and quizzes).</summary>
public sealed record SetWordMembership(Guid StageId, int StageOrder, Guid SetId, int SetOrder, Guid WordId);
