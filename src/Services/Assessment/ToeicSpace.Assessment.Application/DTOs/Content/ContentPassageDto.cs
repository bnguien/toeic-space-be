namespace ToeicSpace.Assessment.Application.DTOs.Content;

public sealed record ContentPassageDto(
    Guid Id,
    ToeicPart Part,
    string? PassageType,
    string? Title,
    string? Content,
    string? ContentTranslation,
    string? AudioUrl,
    string? ImageUrl,
    string? Transcript,
    int OrderIndex,
    IReadOnlyList<ContentQuestionDto> Questions);
