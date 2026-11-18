using ToeicSpace.Identity.Application.Features.Analytics.Common;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;

/// <summary>A validated analytics query: catalogue metric, allowed dimensions and typed filters.</summary>
/// <param name="Range">
/// Resolved dates for metrics that take <c>from</c>/<c>to</c>. Snapshot metrics (only <c>to</c>) get a one-day
/// range on the snapshot date. Null for metrics that read the current state.
/// </param>
/// <param name="Role">Requested role filter, or the role a metric is restricted to (teacher_count, student_count).</param>
public sealed record AnalyticsRequest(
    AnalyticsMetric Metric,
    IReadOnlyList<string> Dimensions,
    AnalyticsDateRange? Range,
    UserRole? Role,
    UserStatus? Status,
    OAuthProvider? Provider,
    int Limit);

public static class AnalyticsRequestParser
{
    /// <summary>Returns null and fills <paramref name="errors"/> when the query is not valid.</summary>
    public static AnalyticsRequest? Parse(
        QueryAnalyticsQuery query,
        DateOnly today,
        ICollection<KeyValuePair<string, string>> errors)
    {
        var errorCount = errors.Count;
        var metricName = AnalyticsValues.Clean(query.Metric)?.ToLowerInvariant();

        if (metricName is null)
        {
            errors.Add(new("metric", "'metric' is required. GET api/v1/analytics/catalog lists the metrics."));
            return null;
        }

        var metric = AnalyticsCatalog.Find(metricName);

        if (metric is null)
        {
            errors.Add(new("metric",
                $"Unknown metric '{AnalyticsValues.Echo(metricName)}'. Allowed: {string.Join(", ", AnalyticsCatalog.Metrics.Select(entry => entry.Name))}."));
            return null;
        }

        var dimensions = ParseDimensions(metric, query.By, errors);

        var supplied = new (string Name, string? Value)[]
        {
            (AnalyticsFilters.From, AnalyticsValues.Clean(query.From)),
            (AnalyticsFilters.To, AnalyticsValues.Clean(query.To)),
            (AnalyticsFilters.Role, AnalyticsValues.Clean(query.Role)),
            (AnalyticsFilters.Status, AnalyticsValues.Clean(query.Status)),
            (AnalyticsFilters.Provider, AnalyticsValues.Clean(query.Provider))
        };

        foreach (var (name, value) in supplied)
        {
            if (value is not null && !metric.Filters.Contains(name))
            {
                errors.Add(new(name, metric.Filters.Count == 0
                    ? $"Metric '{metric.Name}' takes no filters."
                    : $"Filter '{name}' is not supported by metric '{metric.Name}'. Allowed filters: {string.Join(", ", metric.Filters)}."));
            }
        }

        // Filters the metric does not take were reported above and are not parsed again.
        string? Value(string name) => metric.Filters.Contains(name)
            ? supplied.First(filter => filter.Name == name).Value
            : null;

        var range = metric.UsesDateRange
            ? AnalyticsValues.ParseRange(Value(AnalyticsFilters.From), Value(AnalyticsFilters.To), today, errors)
            : null;

        if (range is not null && !metric.UsesFrom)
        {
            range = new AnalyticsDateRange(range.To, range.To);
        }

        var role = AnalyticsValues.ParseRole(AnalyticsFilters.Role, Value(AnalyticsFilters.Role), errors)
            ?? AnalyticsCatalog.FixedRole(metric.Name);
        var status = AnalyticsValues.ParseEnum<UserStatus>(AnalyticsFilters.Status, Value(AnalyticsFilters.Status), errors);
        var provider = AnalyticsValues.ParseEnum<OAuthProvider>(AnalyticsFilters.Provider, Value(AnalyticsFilters.Provider), errors);

        var limit = AnalyticsValues.ParseLimit(query.Limit, AnalyticsLimits.DefaultRowLimit, AnalyticsLimits.MaxRowLimit, errors);

        if (errors.Count > errorCount || limit is null || (metric.UsesDateRange && range is null))
        {
            return null;
        }

        return new AnalyticsRequest(metric, dimensions, range, role, status, provider, limit.Value);
    }

    private static List<string> ParseDimensions(
        AnalyticsMetric metric,
        string? by,
        ICollection<KeyValuePair<string, string>> errors)
    {
        var dimensions = new List<string>();
        var requested = AnalyticsValues.Clean(by)?
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries) ?? [];

        foreach (var raw in requested)
        {
            var dimension = raw.ToLowerInvariant();

            if (!metric.Dimensions.Contains(dimension))
            {
                errors.Add(new("by", AnalyticsDimensions.Descriptions.ContainsKey(dimension)
                    ? $"Dimension '{dimension}' is not supported by metric '{metric.Name}'. Allowed: {string.Join(", ", metric.Dimensions)}."
                    : $"Unknown dimension '{AnalyticsValues.Echo(raw)}'. Allowed for '{metric.Name}': {string.Join(", ", metric.Dimensions)}."));
            }
            else if (dimensions.Contains(dimension))
            {
                errors.Add(new("by", $"Dimension '{dimension}' is listed twice."));
            }
            else
            {
                dimensions.Add(dimension);
            }
        }

        if (metric.RequiredDimension is { } required && !dimensions.Contains(required))
        {
            dimensions.Insert(0, required);
        }

        if (dimensions.Count > AnalyticsLimits.MaxDimensions)
        {
            errors.Add(new("by", metric.RequiredDimension is { } always
                ? $"At most {AnalyticsLimits.MaxDimensions} dimensions are allowed, and metric '{metric.Name}' always groups by '{always}'."
                : $"At most {AnalyticsLimits.MaxDimensions} dimensions are allowed."));
        }

        return dimensions;
    }
}
