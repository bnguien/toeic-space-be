namespace ToeicSpace.Content.Application.Models.News;

/// <summary>List projection of an article (no body, no vocabulary).</summary>
public sealed record NewsArticleSummary(Guid Id, string Slug, string TitleEn, string? TitleVi, string? SummaryVi,
    string SourceName, DateTime? PublishedAt, string Stage, string Topic, int Minutes, int WordCount, string? CoverFile,
    string? CoverAlt, bool Featured);

/// <summary>Filters of the article list; empty strings mean "any". <c>State</c> needs a learner.</summary>
public sealed record NewsArticleFilter(string? Stage, string? Topic, string? Query, string? Length, string? State,
    int Page, int PageSize);

/// <summary>Viewer progress shown on a card.</summary>
public sealed record NewsCardProgress(bool Done, int ReadingSeconds);
