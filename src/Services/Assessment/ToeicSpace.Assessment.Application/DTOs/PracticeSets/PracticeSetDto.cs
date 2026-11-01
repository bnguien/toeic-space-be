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
    DateTime CreatedAt);
