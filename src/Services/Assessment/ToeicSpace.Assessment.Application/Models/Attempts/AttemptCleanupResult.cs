namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <param name="Submitted">Timed attempts auto-submitted after their deadline.</param>
/// <param name="Deleted">Inactive practice attempts removed after the retention period.</param>
public sealed record AttemptCleanupResult(int Submitted, int Deleted)
{
    public const int BatchSize = 100;

    public bool HasMore => Submitted >= BatchSize || Deleted >= BatchSize;
}
