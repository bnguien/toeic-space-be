namespace ToeicSpace.Content.Application.Models.VocabularyImport;

public sealed class VocabularyStageFileWord
{
    public string? Word { get; set; }
    public string? Phonetic { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? MeaningVi { get; set; }
    public string? DefinitionEn { get; set; }
    public string? Example { get; set; }
    public string? ExampleVi { get; set; }
    public string? Cefr { get; set; }
    public List<string>? Synonyms { get; set; }
}
