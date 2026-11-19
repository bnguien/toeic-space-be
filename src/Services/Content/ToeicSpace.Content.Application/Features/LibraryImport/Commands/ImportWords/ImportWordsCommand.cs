using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.LibraryImport;

namespace ToeicSpace.Content.Application.Features.LibraryImport.Commands.ImportWords;

/// <summary>
/// Saves up to 50 items: official words are saved as such, others become (or fill blanks of) the learner's custom
/// words. With <see cref="CreateFoldersFromCategories"/> items go to folders named by their category (reused
/// case-insensitively); other items go to <see cref="FolderId"/> or stay unfiled.
/// </summary>
public sealed record ImportWordsCommand(IReadOnlyList<ImportItemDto> Items, Guid? FolderId = null,
    bool CreateFoldersFromCategories = false, int TzOffset = 0, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<ImportResultDto>
{
    public const int MaxItems = 50;
}
