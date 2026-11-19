namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// The test or practice set an attempt was started from, as it was at start time.
/// </summary>
public sealed record AttemptSourceSnapshot(
    AttemptSourceKind Kind,
    Guid Id,
    string Code,
    string Title,
    string? Category,
    ToeicPart? Part,
    int? Level,
    int? TargetScore,
    int DurationMinutes);
