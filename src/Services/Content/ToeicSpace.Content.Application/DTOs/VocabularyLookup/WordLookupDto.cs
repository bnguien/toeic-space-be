using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.VocabularyLookup;

/// <summary>
/// Enriched word or phrase. <paramref name="Kind"/> is "word" or "phrase"; <paramref name="Sources"/> maps each filled
/// field (camelCase) to cache | catalog | dictionary | google | mymemory | ai; <paramref name="OfficialWordId"/> is set
/// when the text is an official roadmap word (save it with POST me/words instead of creating a custom word).
/// </summary>
public sealed record WordLookupDto(string Text, string Kind, string Headword, string? Phonetic,
    PartOfSpeech? PartOfSpeech, string? MeaningVi, string? DefinitionEn, string? Example, string? ExampleVi,
    string? Cefr, string? PhraseType, string? Usage, string? Explanation, string? LiteralMeaning,
    IReadOnlyDictionary<string, string> Sources, bool Complete, bool AiUsed, ExistingWordDto? Existing,
    Guid? OfficialWordId);
