using System.Text.Json.Serialization;

namespace ToeicSpace.Assessment.Application.DTOs.PracticeSets;

public sealed record PracticeSetDetailDto(
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
    IReadOnlyList<PracticeSetItemDto> Items,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] Guid? CreatedByUserId = null);
