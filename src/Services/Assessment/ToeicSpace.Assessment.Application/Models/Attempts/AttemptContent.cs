namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// An attempt loaded with its immutable snapshot and current answers, in question order.
/// </summary>
public sealed record AttemptContent(
    ToeicAttempt Attempt,
    AttemptSnapshotDocument Document,
    IReadOnlyList<AttemptQuestionEntry> Questions)
{
    public AttemptSourceSnapshot Source => Document.ResolveSource();

    public IReadOnlyDictionary<Guid, PassageSnapshot> PassagesById { get; }
        = Document.Passages.GroupBy(passage => passage.Id).ToDictionary(group => group.Key, group => group.First());
}
