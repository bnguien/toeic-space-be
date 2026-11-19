using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record ActiveTestAttemptDto(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? AttemptId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] AttemptMode? Mode,
    IReadOnlyList<int> Parts,
    int AnsweredCount,
    int TotalQuestions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? Deadline,
    DateTime ServerNow);
