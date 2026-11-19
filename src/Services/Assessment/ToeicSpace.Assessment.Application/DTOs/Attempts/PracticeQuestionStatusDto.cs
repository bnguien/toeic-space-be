using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record PracticeQuestionStatusDto(
    Guid SourceQuestionId,
    int OrderIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? QuestionNumber,
    int Part,
    QuestionProgressStatus Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? LastAnsweredAt);
