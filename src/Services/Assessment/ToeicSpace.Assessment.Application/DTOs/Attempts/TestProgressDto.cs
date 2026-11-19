using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record TestProgressDto(
    Guid TestId,
    TestProgressStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? ActiveAttemptId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] AttemptMode? ActiveMode,
    int AnsweredCount,
    int TotalQuestions,
    int CompletedCount,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? BestTotalScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? LastCompletedAttemptId);
