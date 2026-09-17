namespace ToeicSpace.Assessment.Application.Dtos;

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

public sealed record PracticeSetItemDto(
    Guid QuestionId,
    int OrderIndex);

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
    DateTime? UpdatedAt);

public sealed record PracticeSetContentDto(
    Guid Id,
    string Code,
    string Title,
    string? Description,
    PracticeSetKind Kind,
    ToeicPart Part,
    int? Level,
    int? TargetScore,
    int DurationMinutes,
    int QuestionCount,
    bool IncludesAnswers,
    IReadOnlyList<ContentPartDto> Parts);
