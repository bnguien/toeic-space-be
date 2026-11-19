namespace ToeicSpace.Content.Application.Models.VocabularyImport;

public sealed class VocabularyStageFileStage
{
    public string? Slug { get; set; }
    public string? Title { get; set; }
    public int Order { get; set; }
    public string? Description { get; set; }
    public int? ToeicTarget { get; set; }
}
