namespace ToeicSpace.Assessment.Application.DTOs.Passages;

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
