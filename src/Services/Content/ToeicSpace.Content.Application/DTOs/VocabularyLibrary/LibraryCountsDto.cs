namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

/// <summary>Counts of a group of saved words (the unfiled bucket).</summary>
public sealed record LibraryCountsDto(int WordCount, int Learning, int Mastered, int Due);
