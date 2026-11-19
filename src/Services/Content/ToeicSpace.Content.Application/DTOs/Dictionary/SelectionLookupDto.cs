using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Dictionary;

/// <summary>
/// Result of a "select text to look it up" request. <see cref="Kind"/> is <c>word</c>, <c>phrase</c> (2–5 words,
/// enriched like the vocabulary lookup) or <c>sentence</c> (longer text, machine-translated).
/// <see cref="AiAllowed"/> tells the client whether a second, AI-enabled pass can add anything (signed-in only).
/// </summary>
public sealed record SelectionLookupDto(
    string Text,
    string Kind,
    bool Found,
    string Meaning,
    string Phonetic,
    PartOfSpeech? PartOfSpeech,
    string Cefr,
    string DefinitionEn,
    string Example,
    string ExampleVi,
    string? PhraseType,
    bool Complete,
    bool AiUsed,
    bool AiAllowed,
    Guid? SavedWordId,
    Guid? OfficialWordId);
