namespace ToeicSpace.Assessment.Application.Dtos;

public sealed record PassageDto(
    Guid Id,
    Guid? TestId,
    ToeicPart Part,
    string? PassageType,
    string? Title,
    string? Content,
    string? ContentTranslation,
    string? AudioUrl,
    string? ImageUrl,
    string? Transcript,
    int OrderIndex,
    int QuestionCount,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public sealed record PassageDetailDto(
    Guid Id,
    Guid? TestId,
    ToeicPart Part,
    string? PassageType,
    string? Title,
    string? Content,
    string? ContentTranslation,
    string? AudioUrl,
    string? ImageUrl,
    string? Transcript,
    int OrderIndex,
    IReadOnlyList<QuestionDto> Questions,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
