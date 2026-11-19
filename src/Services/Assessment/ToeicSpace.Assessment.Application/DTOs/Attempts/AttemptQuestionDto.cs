using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <summary>
/// A question as shown while taking an attempt. Deliberately distinct from bank/preview DTOs: it has
/// no answer key, explanation or transcript members at all.
/// </summary>
public sealed record AttemptQuestionDto(
    Guid Id,
    Guid SourceQuestionId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] Guid? PassageId,
    int Part,
    int OrderIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? QuestionNumber,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? QuestionText,
    string OptionA,
    string OptionB,
    string OptionC,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? OptionD,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? AudioUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? ImageUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] AnswerOption? UserAnswer,
    long Revision,
    bool Flagged,
    bool Checked,
    int TimeSpentSeconds);
