namespace ToeicSpace.Assessment.Application.DTOs.PracticeSets;

public sealed record PracticeSetItemDto(
    Guid QuestionId,
    int OrderIndex);
