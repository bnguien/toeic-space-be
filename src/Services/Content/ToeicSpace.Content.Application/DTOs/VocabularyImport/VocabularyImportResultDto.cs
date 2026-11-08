namespace ToeicSpace.Content.Application.DTOs.VocabularyImport;

public sealed record VocabularyImportResultDto(IReadOnlyList<StageImportResultDto> Stages);
