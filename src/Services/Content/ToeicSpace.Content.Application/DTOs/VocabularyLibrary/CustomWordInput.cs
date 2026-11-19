using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.VocabularyLibrary;

/// <summary>Editable fields of a custom word (the <c>custom</c> block of PUT me/words/{id}).</summary>
public sealed record CustomWordInput(string Headword, string? Phonetic, PartOfSpeech PartOfSpeech, string MeaningVi,
    string? DefinitionEn, string? Example, string? ExampleVi);
