using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <summary>
/// The attempt document used by the exam room, the practice workspace and review. Answer-revealing data
/// is only in <see cref="Reveal"/>: checked questions while in progress, everything once completed.
/// Nullable members are always serialized (as null) so clients can rely on every key being present.
/// </summary>
public sealed record AttemptDto(
    Guid Id,
    AttemptMode Mode,
    AttemptStatus Status,
    string Title,
    AttemptSourceDto Source,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? TestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? PracticeSetId,
    IReadOnlyList<int> Parts,
    DateTime StartTime,
    DateTime LastActivityAt,
    DateTime ExpiresAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? Deadline,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? TimeLimitSeconds,
    DateTime ServerNow,
    IReadOnlyList<AttemptQuestionDto> Questions,
    IReadOnlyList<AttemptPassageDto> Passages,
    RevealDto Reveal);
