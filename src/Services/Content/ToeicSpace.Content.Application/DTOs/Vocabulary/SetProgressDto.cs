namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record SetProgressDto(Guid SetId, int WordCount, int Learning, int Mastered, int Due);
