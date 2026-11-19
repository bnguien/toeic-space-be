using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.DTOs.LibraryImport;

/// <summary><paramref name="Created"/> new library entries, <paramref name="Updated"/> entries that already existed
/// (blank fields filled); <paramref name="Items"/> every saved word of the successful rows.</summary>
public sealed record ImportResultDto(int Created, int Updated, IReadOnlyList<ImportFailureDto> Failed,
    IReadOnlyList<SavedWordDto> Items);
