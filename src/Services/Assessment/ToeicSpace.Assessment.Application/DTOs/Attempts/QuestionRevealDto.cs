using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <param name="IsCorrect">Null when the question was skipped.</param>
/// <param name="Transcript">Full stored transcript (Part 3/4 may contain an &lt;hr/&gt; followed by the translation).</param>
/// <param name="History">Graded answers of the same learner to the same source question in other attempts, newest first (max 5).</param>
public sealed record QuestionRevealDto(
    Guid QuestionId,
    AnswerOption CorrectAnswer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] AnswerOption? UserAnswer,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] bool? IsCorrect,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Explanation,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Transcript,
    IReadOnlyList<AnswerHistoryDto> History);
