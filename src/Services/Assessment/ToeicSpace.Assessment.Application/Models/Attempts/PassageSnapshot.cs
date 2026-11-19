namespace ToeicSpace.Assessment.Application.Models.Attempts;

/// <summary>
/// Private, immutable copy of a source passage (including its script and translation).
/// </summary>
public sealed record PassageSnapshot(
    Guid Id,
    ToeicPart Part,
    string? PassageType,
    string? Title,
    string? Content,
    string? Transcript,
    string? AudioUrl,
    string? ImageUrl,
    int OrderIndex);
