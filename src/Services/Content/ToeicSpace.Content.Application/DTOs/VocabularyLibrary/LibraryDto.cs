namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

public sealed record LibraryDto(int Total, LibraryCountsDto Unfiled, IReadOnlyList<FolderDto> Folders);
