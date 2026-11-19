using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.DTOs.LibraryImport;

/// <summary>An <see cref="ImportItemDto"/> plus its source line; <paramref name="Duplicate"/> = repeated in the input,
/// <paramref name="Existing"/> = already in the learner's library.</summary>
public sealed record ImportPreviewItemDto(string Headword, string? MeaningVi, string? Phonetic,
    PartOfSpeech? PartOfSpeech, string? Example, string? ExampleVi, string? Category, int Line, bool Duplicate,
    bool Existing);
