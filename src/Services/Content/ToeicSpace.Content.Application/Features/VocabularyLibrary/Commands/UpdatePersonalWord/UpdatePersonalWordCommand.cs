using System.Text.Json.Serialization;
using ToeicSpace.Content.Application.Common.Json;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdatePersonalWord;

/// <summary>
/// A learner's own edit of a word ("Sửa nghĩa riêng"); only the fields present in the body change.
/// Official word: stored as overrides on the learner's library row (added unfiled when missing); a blank value or one
/// equal to the shared value clears that override and <see cref="Reset"/> clears every override first (the note stays
/// unless sent). Custom word: written straight into the learner's own word. Shared words are never changed.
/// <see cref="PartOfSpeech"/> is a <see cref="Domain.Enums.PartOfSpeech"/> name; blank = back to the shared one.
/// </summary>
public sealed record UpdatePersonalWordCommand(Optional<string?> Phonetic = default,
    Optional<string?> PartOfSpeech = default, Optional<string?> MeaningVi = default, Optional<string?> Example = default,
    Optional<string?> ExampleVi = default, Optional<string?> Note = default, bool Reset = false, int TzOffset = 0,
    [property: JsonIgnore] Guid WordId = default, [property: JsonIgnore] Guid LearnerId = default)
    : IRequest<SavedWordUpsertResult>
{
    /// <summary>The part of speech named by <paramref name="value"/> (case-insensitive), or null when blank or unknown.</summary>
    public static PartOfSpeech? ParsePartOfSpeech(string? value) =>
        Enum.GetNames<PartOfSpeech>().FirstOrDefault(x => string.Equals(x, value?.Trim(),
            StringComparison.OrdinalIgnoreCase)) is { } name ? Enum.Parse<PartOfSpeech>(name) : null;
}
