using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Models.LibraryImport;

/// <summary>Result of <c>VocabularyTextParser</c>: accepted items (1-based source line) and rejected lines.</summary>
public sealed record ParsedImport(IReadOnlyList<ParsedImportItem> Items, IReadOnlyList<ParsedRejectedLine> Rejected);

/// <summary><paramref name="Duplicate"/> marks a later occurrence of the same headword (case-insensitive).</summary>
public sealed record ParsedImportItem(int Line, string Headword, string? MeaningVi, string? Phonetic,
    PartOfSpeech? PartOfSpeech, string? Example, string? ExampleVi, string? Category, bool Duplicate = false);

/// <summary>Codes: <c>no_latin_letter</c>, <c>too_many_words</c>, <c>too_long</c>, <c>invalid_item</c>.</summary>
public sealed record ParsedRejectedLine(int Line, string Text, string Code);
