using System.Globalization;

namespace ToeicSpace.Content.Application.Analytics.Common;

/// <summary>
/// Values of up to two dimensions. Values stay typed (enum, DateOnly, Guid, int, string) so they sort naturally;
/// they are formatted only when the response is written.
/// </summary>
public readonly record struct AnalyticsKey(object? First, object? Second)
{
    public object? this[int index] => index == 0 ? First : Second;

    public static AnalyticsKey For<T>(IReadOnlyList<string> dimensions, T row, Func<string, T, object?> resolve)
        => new(
            dimensions.Count > 0 ? resolve(dimensions[0], row) : null,
            dimensions.Count > 1 ? resolve(dimensions[1], row) : null);
}

/// <param name="Learners">Distinct learners behind the group, used for small-group suppression.</param>
public sealed record AnalyticsGroup(
    AnalyticsKey Key,
    double? Value,
    int Count,
    int Learners);

public static class AnalyticsGrouping
{
    /// <summary>Groups rows by the requested dimensions and measures each group.</summary>
    public static IReadOnlyList<AnalyticsGroup> Aggregate<T>(
        IEnumerable<T> rows,
        IReadOnlyList<string> dimensions,
        Func<string, T, object?> resolve,
        Func<IReadOnlyCollection<T>, (double? Value, int Count, int Learners)> measure)
        => rows
            .GroupBy(row => AnalyticsKey.For(dimensions, row, resolve))
            .Select(group =>
            {
                var (value, count, learners) = measure(group.ToList());
                return new AnalyticsGroup(group.Key, value, count, learners);
            })
            .ToList();

    /// <summary>Time bucket of a UTC timestamp: the day, the Monday of its ISO week, or the first of its month.</summary>
    public static object? TimeBucket(string dimension, DateTime timestamp)
    {
        var day = DateOnly.FromDateTime(timestamp);

        return dimension switch
        {
            AnalyticsDimensions.Day => day,
            AnalyticsDimensions.Week => day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
            AnalyticsDimensions.Month => new DateOnly(day.Year, day.Month, 1),
            _ => null
        };
    }

    public static bool IsTime(string dimension)
        => dimension is AnalyticsDimensions.Day or AnalyticsDimensions.Week or AnalyticsDimensions.Month;

    public static double? Ratio(int numerator, int denominator)
        => denominator == 0 ? null : (double)numerator / denominator;

    public static int Learners<T>(IEnumerable<T> rows, Func<T, Guid> learner)
        => rows.Select(learner).Distinct().Count();

    public static string? Format(string dimension, object? value) => value switch
    {
        null => null,
        DateOnly month when dimension == AnalyticsDimensions.Month => month.ToString("yyyy-MM", CultureInfo.InvariantCulture),
        DateOnly day => day.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        Guid id => id.ToString("D"),
        _ => Convert.ToString(value, CultureInfo.InvariantCulture)
    };

    /// <summary>Nulls last; otherwise the natural order of the typed value (enum number, date, ...).</summary>
    public static int Compare(object? left, object? right)
    {
        if (left is null || right is null)
        {
            return left is null ? (right is null ? 0 : 1) : -1;
        }

        if (left is string leftText && right is string rightText)
        {
            return string.CompareOrdinal(leftText, rightText);
        }

        return left is IComparable comparable && left.GetType() == right.GetType()
            ? comparable.CompareTo(right)
            : string.CompareOrdinal(left.ToString(), right.ToString());
    }
}
