using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One bilingual news article ("Báo song ngữ", natural key <see cref="Slug"/>): an openly licensed English story with
/// a Vietnamese translation per sentence and prepared vocabulary. Body and vocabulary are JSON columns because they
/// are always read and written as a whole. Only <see cref="NewsArticleStatus.Published"/> articles are served.
/// </summary>
public sealed class NewsArticle
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;

    public string SourceName { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string? SourceId { get; set; }
    public string? SourceSeries { get; set; }
    public string? SourceLevel { get; set; }
    public string? Author { get; set; }
    public DateTime? PublishedAt { get; set; }
    /// <summary>Licence key of the text (see <see cref="Rules.NewsCatalog.Licenses"/>).</summary>
    public string License { get; set; } = string.Empty;
    public string Attribution { get; set; } = string.Empty;

    public string TitleEn { get; set; } = string.Empty;
    public string? TitleVi { get; set; }
    public string? SummaryVi { get; set; }
    public List<NewsParagraph> Paragraphs { get; set; } = [];
    public List<NewsVocabularyItem> Vocabulary { get; set; } = [];
    public int WordCount { get; set; }
    public int Minutes { get; set; }

    /// <summary>TOEIC stage key c1..c5.</summary>
    public string Stage { get; set; } = string.Empty;
    /// <summary>Topic slug (see <see cref="Rules.NewsCatalog.Topics"/>).</summary>
    public string Topic { get; set; } = string.Empty;

    /// <summary>Content-addressed cover file name (<c>&lt;hash&gt;.webp</c>) in the cover store; null = no cover.</summary>
    public string? CoverFile { get; set; }
    public string? CoverAlt { get; set; }
    public string? CoverCredit { get; set; }
    public string? CoverLicense { get; set; }
    public string? CoverSourceUrl { get; set; }
    public string? AudioUrl { get; set; }

    public NewsArticleStatus Status { get; set; } = NewsArticleStatus.Draft;
    public bool Featured { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public IEnumerable<NewsSentence> Sentences => Paragraphs.SelectMany(x => x.Sentences);

    public NewsSentence? FindSentence(string sentenceId) => Sentences.FirstOrDefault(x => x.Id == sentenceId);
}

/// <summary>One paragraph (<c>p1</c>, <c>p2</c>…) of an article.</summary>
public sealed class NewsParagraph
{
    public string Id { get; set; } = string.Empty;
    public List<NewsSentence> Sentences { get; set; } = [];
}

/// <summary>One sentence (<c>p1s1</c>…): English text and its Vietnamese translation.</summary>
public sealed class NewsSentence
{
    public string Id { get; set; } = string.Empty;
    public string Text { get; set; } = string.Empty;
    public string Translation { get; set; } = string.Empty;
}

/// <summary>A prepared vocabulary item of an article; <see cref="Level"/> is easy | medium | hard.</summary>
public sealed class NewsVocabularyItem
{
    public string Word { get; set; } = string.Empty;
    public string Ipa { get; set; } = string.Empty;
    /// <summary>Short label: n, v, adj, adv, prep, phr…</summary>
    public string PartOfSpeech { get; set; } = string.Empty;
    /// <summary>Vietnamese meaning.</summary>
    public string Meaning { get; set; } = string.Empty;
    public string Level { get; set; } = "medium";
}
