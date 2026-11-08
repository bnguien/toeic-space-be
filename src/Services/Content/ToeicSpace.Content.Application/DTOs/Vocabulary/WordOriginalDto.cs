using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.Vocabulary;

/// <summary>Shared values of the editable fields of a word the learner edited ("Gốc: …").</summary>
public sealed record WordOriginalDto(string Phonetic, PartOfSpeech PartOfSpeech, string MeaningVi, string? Example,
    string? ExampleVi);
