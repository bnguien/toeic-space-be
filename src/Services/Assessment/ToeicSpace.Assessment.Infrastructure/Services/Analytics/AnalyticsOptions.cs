namespace ToeicSpace.Assessment.Infrastructure.Services.Analytics;

public sealed class AnalyticsOptions
{
    public const string SectionName = "Analytics";

    /// <summary>Keys shorter than this are treated as missing.</summary>
    public const int MinimumPseudonymKeyLength = 32;

    /// <summary>
    /// Server-side HMAC key for learner pseudonyms (configuration key <c>Analytics:PseudonymKey</c>,
    /// environment variable <c>Analytics__PseudonymKey</c>). Keep it secret and stable: changing it
    /// changes every pseudonym.
    /// </summary>
    public string? PseudonymKey { get; init; }
}
