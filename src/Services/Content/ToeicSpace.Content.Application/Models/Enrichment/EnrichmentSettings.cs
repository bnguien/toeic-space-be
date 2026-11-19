namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>Configuration section <c>Enrichment</c>; deadlines bound every request that enriches words.</summary>
public sealed class EnrichmentSettings
{
    public const string SectionName = "Enrichment";

    public int CacheTtlDays { get; set; } = 30;
    /// <summary>Lookup without AI (cache, catalog, dictionary, translation).</summary>
    public int LookupDeadlineSeconds { get; set; } = 6;
    /// <summary>Lookup with AI allowed.</summary>
    public int LookupAiDeadlineSeconds { get; set; } = 12;
    /// <summary>Whole import request (up to 50 items).</summary>
    public int ImportDeadlineSeconds { get; set; } = 20;
    public int AiFillDeadlineSeconds { get; set; } = 12;
}
