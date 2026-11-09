namespace ToeicSpace.Content.Application.DTOs.LibraryImport;

public sealed record ImportPreviewDto(IReadOnlyList<ImportPreviewItemDto> Items,
    IReadOnlyList<ImportRejectedLineDto> Rejected);
