namespace ToeicSpace.Identity.Application.Features.Analytics.Results;

/// <summary>Everything a caller needs to build a valid analytics query against the Identity service.</summary>
public sealed record AnalyticsCatalogDto(
    string Service,
    IReadOnlyList<AnalyticsMetricDto> Metrics,
    IReadOnlyList<AnalyticsNameDto> Dimensions,
    IReadOnlyList<AnalyticsNameDto> Filters,
    AnalyticsLimitsDto Limits);

/// <param name="CountUnit">What <c>n</c> counts in a result row.</param>
/// <param name="RequiredDimension">Always part of the grouping, added when missing from <c>by</c>.</param>
/// <param name="Personal">Computed per account before being aggregated.</param>
/// <param name="Suppressed">Groups with fewer than the minimum number of distinct accounts return a null value.</param>
/// <param name="Sort">dimensions, value_asc or value_desc.</param>
public sealed record AnalyticsMetricDto(
    string Name,
    string Description,
    string Unit,
    string CountUnit,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Filters,
    string? RequiredDimension,
    string Visibility,
    bool Personal,
    bool Suppressed,
    string Sort);

public sealed record AnalyticsNameDto(
    string Name,
    string Description);

public sealed record AnalyticsLimitsDto(
    int MinimumAccountsPerGroup,
    int MaxDimensions,
    int DefaultRowLimit,
    int MaxRowLimit,
    int DefaultRangeDays,
    int MaxRangeDays,
    int InactivityDays,
    int TimeoutSeconds);

/// <param name="From">Start of the range; null for snapshot metrics, which only read <c>to</c> (or now).</param>
/// <param name="To">End of the range or date of the snapshot; null for metrics that read the current state.</param>
/// <param name="Rows">One object per group: each dimension of <c>by</c>, then <c>value</c> (null when suppressed) and <c>n</c> (0 when suppressed).</param>
/// <param name="Suppressed">Number of groups hidden because they hold fewer than 5 distinct accounts.</param>
/// <param name="Truncated">True when more groups existed than <c>limit</c>.</param>
/// <param name="Labels">Always empty: Identity rows only hold enum names and dates.</param>
public sealed record AnalyticsResultDto(
    string Metric,
    IReadOnlyList<string> By,
    string Unit,
    string CountUnit,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int Suppressed,
    bool Truncated,
    DateTime GeneratedAt,
    IReadOnlyDictionary<string, string> Labels);
