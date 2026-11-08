using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary>
/// A word as the caller sees it. For a signed-in learner the personal edits are already overlaid:
/// <paramref name="Note"/> is the library note, <paramref name="Personal"/> lists the overridden fields
/// (phonetic, partOfSpeech, meaningVi, example, exampleVi) and <paramref name="Original"/> holds the shared values
/// of the word while <paramref name="Personal"/> is not empty. All three are null for guests and unedited words.
/// </summary>
public sealed record WordDto(Guid Id, string Headword, string Phonetic, PartOfSpeech PartOfSpeech, string MeaningVi,
    string? DefinitionEn, string? Example, string? ExampleVi, string? Cefr, IReadOnlyList<string> Synonyms,
    string? Note = null, IReadOnlyList<string>? Personal = null, WordOriginalDto? Original = null);
