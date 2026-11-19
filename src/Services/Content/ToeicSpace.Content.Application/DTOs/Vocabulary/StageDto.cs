namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record StageDto(Guid Id, string Slug, string Title, string? Description, int Order,
    int? ToeicTarget, int SetCount, int WordCount);
