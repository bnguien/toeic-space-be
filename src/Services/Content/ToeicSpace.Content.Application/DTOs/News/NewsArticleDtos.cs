namespace ToeicSpace.Content.Application.DTOs.News;

/// <summary>
/// Card of the article list (no body). <paramref name="Done"/> and <paramref name="ReadingSeconds"/> are the
/// signed-in viewer's (false / null for guests); <paramref name="CoverUrl"/> is a gateway path.
/// </summary>
public sealed record NewsArticleCardDto(string Slug, string TitleEn, string? TitleVi, string? SummaryVi,
    string SourceName, DateTime? PublishedAt, string Stage, string Topic, int Minutes, int WordCount, string? CoverUrl,
    string? CoverAlt, bool Featured, bool Done, int? ReadingSeconds);

/// <summary>Published article count per stage key and per topic slug.</summary>
public sealed record NewsFacetsDto(IReadOnlyDictionary<string, int> Stage, IReadOnlyDictionary<string, int> Topic);

/// <summary>One page of cards + totals; <c>DoneCount</c> / <c>ReadingCount</c> are the viewer's (0 for guests).</summary>
public sealed record NewsArticleListDto(IReadOnlyList<NewsArticleCardDto> Items, int Total, int Page, int PageSize,
    NewsFacetsDto Facets, int DoneCount, int ReadingCount);

public sealed record NewsSentenceDto(string Id, string Text, string Translation);

public sealed record NewsParagraphDto(string Id, IReadOnlyList<NewsSentenceDto> Sentences);

public sealed record NewsVocabularyItemDto(string Word, string Ipa, string PartOfSpeech, string Meaning, string Level);

public sealed record NewsArticleDetailDto(string Slug, string TitleEn, string? TitleVi, string? SummaryVi, string Stage,
    string Topic, int Minutes, int WordCount, string SourceName, string SourceUrl, string? SourceSeries, string? Author,
    DateTime? PublishedAt, string License, string Attribution, string? CoverUrl, string? CoverAlt, string? CoverCredit,
    string? CoverLicense, string? CoverSourceUrl, string? AudioUrl, IReadOnlyList<NewsParagraphDto> Paragraphs,
    IReadOnlyList<NewsVocabularyItemDto> Vocabulary);
