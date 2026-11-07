namespace ToeicSpace.Content.Application.DTOs.VocabularyImport;

/// <summary>Per-stage import counts; <c>Unchanged</c> counts sets and words that already matched the file.</summary>
public sealed record StageImportResultDto(string Slug, int SetsCreated, int SetsUpdated, int WordsCreated,
    int WordsUpdated, int Unchanged);
