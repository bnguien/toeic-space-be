using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateFolder;

/// <summary>Renames and/or moves a folder; omitted fields stay unchanged.</summary>
public sealed record UpdateFolderCommand(string? Name, int? Order, int TzOffset = 0,
    [property: JsonIgnore] Guid FolderId = default, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<FolderDto>;
