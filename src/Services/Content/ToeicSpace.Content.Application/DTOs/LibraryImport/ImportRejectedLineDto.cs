namespace ToeicSpace.Content.Application.DTOs.LibraryImport;

/// <summary>Codes: no_latin_letter, too_many_words, too_long, invalid_item.</summary>
public sealed record ImportRejectedLineDto(int Line, string Text, string Code);
