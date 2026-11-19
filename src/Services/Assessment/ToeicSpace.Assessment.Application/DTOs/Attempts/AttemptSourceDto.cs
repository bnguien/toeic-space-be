using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record AttemptSourceDto(
    AttemptSourceKind Kind,
    Guid Id,
    string Code,
    string Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] string? Category,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Part,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? Level,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.Never)] int? TargetScore);
