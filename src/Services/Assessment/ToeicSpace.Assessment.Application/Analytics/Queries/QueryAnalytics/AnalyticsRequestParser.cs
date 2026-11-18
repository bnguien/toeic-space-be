using ToeicSpace.Assessment.Application.Analytics.Common;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

/// <summary>A validated analytics query: catalogue metric, allowed dimensions and typed filters.</summary>
public sealed record AnalyticsRequest(
    AnalyticsMetric Metric,
    IReadOnlyList<string> Dimensions,
    AnalyticsDateRange? Range,
    Guid? TestId,
    ToeicPart? Part,
    ToeicSection? Section,
    QuestionDifficulty? Difficulty,
    AttemptMode? Mode,
    AttemptStatus? AttemptStatus,
    ContentStatus? ContentStatus,
    int Limit)
{
    public bool Has(string dimension) => Dimensions.Contains(dimension);
}

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
            errors.Add(new("metric", AnalyticsCatalog.IsPlanned(metricName)
                ? $"Metric '{metricName}' is planned for another service and not available yet."
                : $"Unknown metric '{AnalyticsValues.Echo(metricName)}'. GET api/v1/analytics/catalog lists the metrics."));
            return null;
        }

        var dimensions = ParseDimensions(metric, query.By, errors);

        var supplied = new (string Name, string? Value)[]
        {
            (AnalyticsFilters.From, AnalyticsValues.Clean(query.From)),
            (AnalyticsFilters.To, AnalyticsValues.Clean(query.To)),
            (AnalyticsFilters.TestId, AnalyticsValues.Clean(query.TestId)),
            (AnalyticsFilters.Part, AnalyticsValues.Clean(query.Part)),
            (AnalyticsFilters.Section, AnalyticsValues.Clean(query.Section)),
            (AnalyticsFilters.Difficulty, AnalyticsValues.Clean(query.Difficulty)),
            (AnalyticsFilters.Status, AnalyticsValues.Clean(query.Status)),
            (AnalyticsFilters.Mode, AnalyticsValues.Clean(query.Mode))
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

        var testId = AnalyticsValues.ParseGuid(AnalyticsFilters.TestId, Value(AnalyticsFilters.TestId), errors);
        var part = AnalyticsValues.ParseEnum<ToeicPart>(AnalyticsFilters.Part, Value(AnalyticsFilters.Part), errors);
        var section = AnalyticsValues.ParseEnum<ToeicSection>(AnalyticsFilters.Section, Value(AnalyticsFilters.Section), errors);
        var difficulty = AnalyticsValues.ParseEnum<QuestionDifficulty>(AnalyticsFilters.Difficulty, Value(AnalyticsFilters.Difficulty), errors);
        var mode = AnalyticsValues.ParseEnum<AttemptMode>(AnalyticsFilters.Mode, Value(AnalyticsFilters.Mode), errors);

        // The status filter means the attempt status for learner metrics and the content status for bank metrics.
        var status = Value(AnalyticsFilters.Status);
        var attemptStatus = metric.Source == AnalyticsSource.Bank
            ? null
            : AnalyticsValues.ParseEnum<AttemptStatus>(AnalyticsFilters.Status, status, errors);
        var contentStatus = metric.Source == AnalyticsSource.Bank
            ? AnalyticsValues.ParseEnum<ContentStatus>(AnalyticsFilters.Status, status, errors)
            : null;

        var limit = AnalyticsValues.ParseLimit(query.Limit, AnalyticsLimits.DefaultRowLimit, AnalyticsLimits.MaxRowLimit, errors);

        if (errors.Count > errorCount || limit is null || (metric.UsesDateRange && range is null))
        {
            return null;
        }

        return new AnalyticsRequest(
            metric,
            dimensions,
            range,
            testId,
            part,
            section,
            difficulty,
            mode,
            attemptStatus,
            contentStatus,
            limit.Value);
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
