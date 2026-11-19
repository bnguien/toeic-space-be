using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <summary>
/// Scaled scores are only set when the attempt covers a whole section of a test (Parts 1–4 or 5–7);
/// the total and the CEFR band only when both sections are covered.
/// </summary>
public sealed record AttemptScoreDto(
    int QuestionCount,
    int Answered,
    int Correct,
    int ListeningCorrect,
    int ListeningTotal,
    int ReadingCorrect,
    int ReadingTotal,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ListeningScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ReadingScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? TotalScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] CefrDto? Cefr);
