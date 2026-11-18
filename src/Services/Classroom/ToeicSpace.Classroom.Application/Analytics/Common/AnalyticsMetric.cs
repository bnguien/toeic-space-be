namespace ToeicSpace.Classroom.Application.Analytics.Common;

/// <summary>Which data a metric is computed from; decides the meaning of the <c>status</c> filter.</summary>
public enum AnalyticsSource
{
    /// <summary>Classes; <c>status</c> is the class status.</summary>
    Classes,

    /// <summary>Enrollments; <c>status</c> is the enrollment status.</summary>
    Enrollments,

    /// <summary>Classes started in the range with their enrollments.</summary>
    ClassCohorts,

    /// <summary>Current teacher assignments.</summary>
    Teachers
}

public enum AnalyticsSort
{
    Dimensions,
    ValueAscending,
    ValueDescending
}

/// <param name="CountUnit">What <c>n</c> counts in a result row.</param>
/// <param name="Suppressed">True when the metric is derived from learners and small groups must be hidden.</param>
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
    public bool UsesDateRange => Filters.Contains(AnalyticsFilters.From);
}

/// <summary>A metric listed for discovery that is not served yet.</summary>
public sealed record PlannedAnalyticsMetric(
    string Name,
    string Service,
    string Description,
    string Visibility = AnalyticsVisibility.Admin);
