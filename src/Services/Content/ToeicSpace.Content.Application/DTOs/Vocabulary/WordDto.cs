using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

public sealed record WordDto(Guid Id, string Headword, string Phonetic, PartOfSpeech PartOfSpeech, string MeaningVi,
    string? DefinitionEn, string? Example, string? ExampleVi, string? Cefr, IReadOnlyList<string> Synonyms);
