using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.LibraryImport;

public sealed record ImportItemDto(string Headword, string? MeaningVi = null, string? Phonetic = null,
    PartOfSpeech? PartOfSpeech = null, string? Example = null, string? ExampleVi = null, string? Category = null);
