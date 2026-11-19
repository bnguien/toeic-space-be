using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record AnswerHistoryDto(
    Guid AttemptId,
    DateTime AnsweredAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] AnswerOption? UserAnswer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool? IsCorrect);
