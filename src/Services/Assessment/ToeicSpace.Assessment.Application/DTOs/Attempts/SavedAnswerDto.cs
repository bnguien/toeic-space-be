using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record SavedAnswerDto(
    Guid QuestionId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] AnswerOption? UserAnswer,
    long Revision,
    DateTime LastActivityAt,
    int TimeSpentSeconds);
