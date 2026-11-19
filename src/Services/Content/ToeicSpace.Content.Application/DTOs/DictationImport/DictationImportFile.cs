namespace ToeicSpace.Content.Application.DTOs.DictationImport;

/// <summary>An uploaded pack file; <paramref name="Content"/> is already decompressed when it was a .json.gz.</summary>
public sealed record DictationImportFile(string FileName, long SizeBytes, string Content);

public sealed record DictationPackImportResultDto(string Pack, int CoursesCreated, int CoursesUpdated,
    int ItemsCreated, int ItemsUpdated, int Deactivated, int Unchanged);

public sealed record DictationImportResultDto(IReadOnlyList<DictationPackImportResultDto> Packs);
