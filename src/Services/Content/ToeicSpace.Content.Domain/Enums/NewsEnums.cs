namespace ToeicSpace.Content.Domain.Enums;

/// <summary>Publication state of a news article; only <see cref="Published"/> is served to learners.</summary>
public enum NewsArticleStatus
{
    /// <summary>Fetched, not fully translated yet.</summary>
    Draft,
    /// <summary>Translated, waiting for review.</summary>
    Review,
    Published,
    Hidden
}
