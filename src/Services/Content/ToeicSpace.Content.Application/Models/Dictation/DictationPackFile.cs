namespace ToeicSpace.Content.Application.Models.Dictation;

/// <summary>Shape of <c>data/dictation/&lt;pack&gt;.json</c> (schemaVersion 1), written by the data pipeline.</summary>
public sealed class DictationPackFile
{
    public int SchemaVersion { get; set; }
    public string? Source { get; set; }
    public string? Pack { get; set; }
    public int? RoadmapLevel { get; set; }
    public List<DictationPackFileCourse>? Courses { get; set; }
}

public sealed class DictationPackFileCourse
{
    public string? Code { get; set; }
    public int Order { get; set; }
    public int Level { get; set; }
    public string? Title { get; set; }
    public double AvgWords { get; set; }
    public int VocabCount { get; set; }
    public List<DictationPackFileItem>? Items { get; set; }
}

public sealed class DictationPackFileItem
{
    public string? SourceCode { get; set; }
    public int Order { get; set; }
    public string? Topic { get; set; }
    public string? Text { get; set; }
    public string? Translation { get; set; }
    public string? Ipa { get; set; }
    public string? Voice { get; set; }
    public int Rate { get; set; }
    public int? MaxPlays { get; set; }
    public List<DictationPackFileKeyword>? Keywords { get; set; }
    public List<DictationPackFileNote>? Notes { get; set; }
    public List<DictationPackFileVocab>? Vocab { get; set; }
}

public sealed class DictationPackFileKeyword
{
    public string? Word { get; set; }
    public string? Meaning { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? Ipa { get; set; }
    public string? Cefr { get; set; }
}

public sealed class DictationPackFileNote
{
    public string? Id { get; set; }
    public string? Text { get; set; }
    public string? Type { get; set; }
    public string? Explanation { get; set; }
}

public sealed class DictationPackFileVocab
{
    public string? Word { get; set; }
    public string? Lemma { get; set; }
    public string? Meaning { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? Ipa { get; set; }
}
