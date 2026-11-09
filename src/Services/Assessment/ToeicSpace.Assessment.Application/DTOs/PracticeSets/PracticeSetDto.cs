using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.PracticeSets;

public sealed record PracticeSetDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    PracticeSetKind Kind,
    ToeicPart Part,
    int? Level,
    int? TargetScore,
    int DurationMinutes,
    ContentStatus Status,
    int OrderIndex,
    int QuestionCount,
    DateTime CreatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CreatedByUserId = null);
