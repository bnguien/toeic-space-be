namespace ToeicSpace.Content.Application.Models.VocabularyImport;

/// <summary>Root of a <c>data/vocabulary/NN-*.json</c> stage file (schemaVersion 1).</summary>
public sealed class VocabularyStageFile
{
    public int SchemaVersion { get; set; }
    public string? Source { get; set; }
    public VocabularyStageFileStage? Stage { get; set; }
    public List<VocabularyStageFileSet>? Sets { get; set; }
}
