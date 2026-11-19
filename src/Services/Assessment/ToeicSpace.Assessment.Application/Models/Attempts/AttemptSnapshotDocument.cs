namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// Payload of <see cref="ToeicAttemptSnapshot"/>. Practice set attempts keep <see cref="Set"/> (the original
/// schema 1 shape); test attempts only have <see cref="Source"/>. Older payloads without Source still deserialize.
/// </summary>
public sealed record AttemptSnapshotDocument(
    SetSnapshot? Set,
    IReadOnlyList<PassageSnapshot> Passages,
    AttemptSourceSnapshot? Source = null)
{
    public AttemptSourceSnapshot ResolveSource()
        => Source
            ?? (Set is { } set
                ? new AttemptSourceSnapshot(AttemptSourceKind.PracticeSet, set.Id, set.Code, set.Title, null,
                    set.Part, set.Level, set.TargetScore, set.DurationMinutes)
                : throw new InvalidOperationException("Attempt snapshot has no source."));
}
