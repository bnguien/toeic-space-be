namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

public sealed record QuestionFlagDto(
    Guid QuestionId,
    bool Flagged);
