namespace ToeicSpace.Content.Application.DTOs.Videos;

/// <summary>Catalog rows, subtitle documents (one per video) and tracks created/updated/unchanged; <c>files</c> = files read.</summary>
public sealed record VideoImportResultDto(ImportCountsDto Videos, ImportCountsDto Subtitles, ImportCountsDto Tracks,
    IReadOnlyList<string> Files);
