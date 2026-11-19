namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// Practice set metadata captured when an attempt starts (snapshot schema 1).
/// </summary>
public sealed record SetSnapshot(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    ToeicPart Part,
    PracticeSetKind Kind,
    int? Level,
    int? TargetScore,
    int DurationMinutes);
