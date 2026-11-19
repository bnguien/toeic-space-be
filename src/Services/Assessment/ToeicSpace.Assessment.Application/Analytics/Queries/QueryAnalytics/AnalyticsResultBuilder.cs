using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Groups after suppression, sorting and the row limit.</summary>
public sealed record AnalyticsSelection(
    IReadOnlyList<AnalyticsGroup> Rows,
    int Suppressed,
    bool Truncated);

public static class AnalyticsResultBuilder
{
    private static readonly HashSet<string> CountUnits =
        ["attempts", "learners", "answers", "questions", "passages", "tests", "sets"];

    /// <summary>
    /// Hides groups with fewer than <see cref="AnalyticsLimits.MinimumLearnersPerGroup"/> distinct learners
    /// (value null, n 0), sorts and applies the limit. Rankings list suppressed groups last.
    /// </summary>
    public static AnalyticsSelection Select(
        AnalyticsRequest request,
        IReadOnlyList<AnalyticsGroup> groups)
    {
        var metric = request.Metric;

        // Without dimensions there is always exactly one row, even when nothing matched.
        if (request.Dimensions.Count == 0 && groups.Count == 0)
        {
            groups = [new AnalyticsGroup(default, CountUnits.Contains(metric.Unit) ? 0 : null, 0, 0)];
        }

        var shaped = groups
            .Select(group =>
            {
                var hidden = metric.Suppressed && group.Learners < AnalyticsLimits.MinimumLearnersPerGroup;
                return (
                    Group: hidden ? group with { Value = null, Count = 0 } : group with { Value = Round(group.Value, metric.Unit) },
                    Hidden: hidden);
            })
            .ToList();

        // Rankings put hidden groups last; dimension order keeps them in place so gaps stay visible.
        var ordered = shaped.OrderBy(row => metric.Sort != AnalyticsSort.Dimensions && row.Hidden);

        ordered = metric.Sort switch
        {
            AnalyticsSort.ValueAscending => ordered
                .ThenBy(row => row.Group.Value is null)
                .ThenBy(row => row.Group.Value)
                .ThenByDescending(row => row.Group.Count),
            AnalyticsSort.ValueDescending => ordered
                .ThenBy(row => row.Group.Value is null)
                .ThenByDescending(row => row.Group.Value)
                .ThenByDescending(row => row.Group.Count),
            _ => ordered
        };

        var keyOrder = Comparer<object?>.Create(AnalyticsGrouping.Compare);
        var rows = ordered
            .ThenBy(row => row.Group.Key.First, keyOrder)
            .ThenBy(row => row.Group.Key.Second, keyOrder)
            .Take(request.Limit)
            .Select(row => row.Group)
            .ToList();

        var suppressed = shaped.Count(row => row.Hidden);

        return new AnalyticsSelection(rows, suppressed, shaped.Count > request.Limit);
    }

    public static AnalyticsResultDto ToDto(
        AnalyticsRequest request,
        AnalyticsSelection selection,
        IReadOnlyDictionary<string, string>? labels,
        DateTime generatedAt)
    {
        var dimensions = request.Dimensions;

        var rows = selection.Rows
            .Select(group =>
            {
                var row = new Dictionary<string, object?>(dimensions.Count + 2);

                for (var index = 0; index < dimensions.Count; index++)
                {
                    row[dimensions[index]] = AnalyticsGrouping.Format(dimensions[index], group.Key[index]);
                }

                row["value"] = group.Value;
                row["n"] = group.Count;
                return (IReadOnlyDictionary<string, object?>)row;
            })
            .ToList();

        return new AnalyticsResultDto(
            request.Metric.Name,
            dimensions,
            request.Metric.Unit,
            request.Metric.CountUnit,
            request.Range?.From,
            request.Range?.To,
            rows,
            selection.Suppressed,
            selection.Truncated,
            DateTime.SpecifyKind(generatedAt, DateTimeKind.Utc),
            labels is { Count: > 0 } ? labels : null);
    }

    /// <summary>Distinct IDs of a dimension among the selected rows (for labels).</summary>
    public static List<Guid> IdsOf(AnalyticsRequest request, AnalyticsSelection selection, string dimension)
    {
        var index = request.Dimensions.ToList().IndexOf(dimension);

        return index < 0
            ? []
            : selection.Rows
                .Select(group => group.Key[index])
                .OfType<Guid>()
                .Distinct()
                .ToList();
    }

    private static double? Round(double? value, string unit) => value switch
    {
        null => null,
        _ when unit == "ratio" => Math.Round(value.Value, 4),
        _ => Math.Round(value.Value, 2)
    };
}
