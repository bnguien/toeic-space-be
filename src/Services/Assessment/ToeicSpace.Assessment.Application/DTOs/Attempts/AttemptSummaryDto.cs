using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record AttemptSummaryDto(
    Guid Id,
    AttemptMode Mode,
    AttemptStatus Status,
    string Title,
    AttemptSourceDto Source,
    IReadOnlyList<int> Parts,
    DateTime StartTime,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? EndTime,
    int DurationSeconds,
    int QuestionCount,
    int Answered,
    int Correct,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ListeningScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ReadingScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? TotalScaled);
