namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

/// <summary><paramref name="Mistakes"/> = pending mistakes of the learner ("Từ sai"), any word.</summary>
public sealed record LibraryDto(int Total, LibraryCountsDto Unfiled, IReadOnlyList<FolderDto> Folders, int Mistakes = 0);
