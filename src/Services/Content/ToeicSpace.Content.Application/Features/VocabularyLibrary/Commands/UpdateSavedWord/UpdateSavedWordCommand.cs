using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateSavedWord;

/// <summary>
/// Moves a saved word (<c>"folderId": null</c> = unfiled), edits its note and, for custom words only, the word itself.
/// Omitted fields stay unchanged.
/// </summary>
public sealed record UpdateSavedWordCommand(Optional<Guid?> FolderId = default, Optional<string?> Note = default,
    CustomWordInput? Custom = null, int TzOffset = 0, [property: JsonIgnore] Guid SavedWordId = default,
    [property: JsonIgnore] Guid LearnerId = default) : IRequest<SavedWordDto>;
