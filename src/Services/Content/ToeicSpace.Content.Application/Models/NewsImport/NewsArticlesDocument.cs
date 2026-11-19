namespace ToeicSpace.Content.Application.Models.NewsImport;

/// <summary>Shape of <c>data/news/articles.json</c> (schemaVersion 1), written by scripts/news_data_pipeline.py.</summary>
public sealed class NewsArticlesDocument
{
    public int SchemaVersion { get; set; }
    public List<NewsArticleFileItem>? Articles { get; set; }
}

public sealed class NewsArticleFileItem
{
    public string? Slug { get; set; }
    /// <summary>draft | review | published | hidden (default draft).</summary>
    public string? Status { get; set; }
    public bool Featured { get; set; }
    public NewsArticleFileSource? Source { get; set; }
    public string? TitleEn { get; set; }
    public string? TitleVi { get; set; }
    public string? SummaryVi { get; set; }
    public string? Stage { get; set; }
    public string? Topic { get; set; }
    public List<NewsArticleFileParagraph>? Paragraphs { get; set; }
    public List<NewsArticleFileVocabulary>? Vocabulary { get; set; }
    public NewsArticleFileCover? Cover { get; set; }
    public string? AudioUrl { get; set; }
}

public sealed class NewsArticleFileSource
{
    public string? Name { get; set; }
    public string? Url { get; set; }
    public string? Id { get; set; }
    public string? Series { get; set; }
    public string? Level { get; set; }
    public string? Author { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public string? License { get; set; }
    public string? Attribution { get; set; }
}

public sealed class NewsArticleFileParagraph
{
    public List<NewsArticleFileSentence>? Sentences { get; set; }
}

public sealed class NewsArticleFileSentence
{
    public string? Text { get; set; }
    public string? Translation { get; set; }
}

public sealed class NewsArticleFileVocabulary
{
    public string? Word { get; set; }
    public string? Ipa { get; set; }
    public string? PartOfSpeech { get; set; }
    public string? Meaning { get; set; }
    public string? Level { get; set; }
}

public sealed class NewsArticleFileCover
{
    public string? File { get; set; }
    public string? Alt { get; set; }
    public string? Credit { get; set; }
    public string? License { get; set; }
    public string? SourceUrl { get; set; }
}
