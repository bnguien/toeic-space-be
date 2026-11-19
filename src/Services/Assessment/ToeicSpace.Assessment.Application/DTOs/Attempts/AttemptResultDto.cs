using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record AttemptResultDto(
    Guid AttemptId,
    AttemptMode Mode,
    AttemptStatus Status,
    string Title,
    AttemptSourceDto Source,
    IReadOnlyList<int> Parts,
    DateTime StartTime,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] DateTime? EndTime,
    int DurationSeconds,
    AttemptScoreDto Score,
    IReadOnlyList<PartScoreDto> ByPart,
    IReadOnlyList<AnswerResultDto> Answers);
