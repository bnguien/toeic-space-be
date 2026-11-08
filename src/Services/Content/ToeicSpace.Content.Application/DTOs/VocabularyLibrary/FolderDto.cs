namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

public sealed record FolderDto(Guid Id, string Name, int Order, int WordCount, int Learning, int Mastered, int Due);
