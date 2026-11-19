using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record ScoreTrendPointDto(
    Guid AttemptId,
    DateTime CompletedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? TotalScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ListeningScaled,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? ReadingScaled);
