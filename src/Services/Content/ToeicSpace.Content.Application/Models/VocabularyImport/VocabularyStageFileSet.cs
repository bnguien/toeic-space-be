namespace ToeicSpace.Content.Application.Models.VocabularyImport;

public sealed class VocabularyStageFileSet
{
    public string? Slug { get; set; }
    public string? Title { get; set; }
    public int Order { get; set; }
    public string? Topic { get; set; }
    public List<VocabularyStageFileWord>? Words { get; set; }
}
