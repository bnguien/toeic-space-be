using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record AnswerResultDto(
    Guid QuestionId,
    Guid SourceQuestionId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? QuestionNumber,
    int Part,
    int OrderIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] AnswerOption? UserAnswer,
    AnswerOption CorrectAnswer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool? IsCorrect,
    bool Flagged);
