namespace ToeicSpace.Content.Application.DTOs.LibraryImport;

/// <summary><paramref name="Index"/> is the 0-based position in the request's items.</summary>
public sealed record ImportFailureDto(int Index, string Headword, string Code);
