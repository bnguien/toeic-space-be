namespace ToeicSpace.Identity.Application.Features.Analytics.Common;

/// <summary>Which data a metric is computed from.</summary>
public enum AnalyticsSource
{
    /// <summary>Accounts created in the date range.</summary>
    Registrations,

    /// <summary>Accounts existing now, or at <c>to</c> when the metric takes that filter.</summary>
    Accounts,

    /// <summary>Accounts linked to an external sign-in provider.</summary>
    OAuthLinks,

    /// <summary>Refresh tokens issued in the date range (sign-ins and session renewals).</summary>
    SignIns,

    /// <summary>Recent sign-in activity of each account up to <c>to</c>.</summary>
    Activity
}

public enum AnalyticsSort
{
    Dimensions,
    ValueAscending,
    ValueDescending
}

/// <param name="CountUnit">What <c>n</c> counts in a result row.</param>
/// <param name="Personal">Computed per account before being aggregated.</param>
/// <param name="Suppressed">True when groups with fewer than the minimum number of distinct accounts are hidden.</param>
/// <param name="RequiredDimension">Dimension that is always part of the grouping (added when missing from <c>by</c>).</param>
public sealed record AnalyticsMetric(
    string Name,
    string Description,
    string Unit,
    string CountUnit,
    AnalyticsSource Source,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Filters,
    string Visibility = AnalyticsVisibility.Admin,
    bool Personal = false,
    bool Suppressed = false,
    string? RequiredDimension = null,
    AnalyticsSort Sort = AnalyticsSort.Dimensions)
{
    /// <summary>The metric takes <c>from</c> and/or <c>to</c>, so a date range is resolved for it.</summary>
    public bool UsesDateRange => Filters.Contains(AnalyticsFilters.From) || Filters.Contains(AnalyticsFilters.To);

    /// <summary>The metric counts events inside <c>[from, to]</c> rather than a snapshot at <c>to</c>.</summary>
    public bool UsesFrom => Filters.Contains(AnalyticsFilters.From);
}
