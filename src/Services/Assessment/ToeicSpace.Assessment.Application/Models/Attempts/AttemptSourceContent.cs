namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// Live content an attempt is about to snapshot. <see cref="Order"/> is the question order of the source;
/// <see cref="Questions"/> and <see cref="Passages"/> are sorted by ID so two reads compare equal.
/// </summary>
public sealed record AttemptSourceContent(
    ToeicTest? Test,
    ToeicPracticeSet? Set,
    IReadOnlyList<ToeicQuestion> Questions,
    IReadOnlyList<ToeicPassage> Passages,
    IReadOnlyList<Guid> Order);
