namespace ToeicSpace.Content.Application.DTOs.StudySessions;

/// <summary>Remembered = last grade Good/Easy; the rest (Forget, Hard, Seen) need review.</summary>
public sealed record SessionSummaryDto(int Remembered, int NeedReview, IReadOnlyList<Guid> WordIds);
