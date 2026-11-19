using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;

/// <summary>
/// Saves an official word (idempotent). For a word already in the library, only the fields present in the body
/// change; <c>"folderId": null</c> moves it to unfiled.
/// </summary>
public sealed record SaveWordCommand(Guid WordId, Optional<Guid?> FolderId = default, Optional<string?> Note = default,
    int TzOffset = 0, [property: JsonIgnore] Guid LearnerId = default) : IRequest<SavedWordUpsertResult>;
