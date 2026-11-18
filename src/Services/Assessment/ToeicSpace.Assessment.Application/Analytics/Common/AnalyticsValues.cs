using System.Globalization;

namespace ToeicSpace.Assessment.Application.Analytics.Common;

/// <summary>Inclusive UTC date range of an analytics request.</summary>
public sealed record AnalyticsDateRange(DateOnly From, DateOnly To)
{
    public DateTime FromUtc => From.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

    public DateTime ToUtcExclusive => To.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
}

/// <summary>
/// Parses query-string values of the analytics endpoints. Failures are collected as (parameter, message)
/// pairs so every problem is reported in one 400 response.
/// </summary>
public static class AnalyticsValues
{
    private const int MaxEchoLength = 40;

    public static DateOnly Today(TimeProvider timeProvider)
        => DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

    public static AnalyticsDateRange? ParseRange(
        string? from,
        string? to,
        DateOnly today,
        ICollection<KeyValuePair<string, string>> errors)
    {
        var start = ParseDate(AnalyticsFilters.From, from, errors);
        var end = ParseDate(AnalyticsFilters.To, to, errors);

        if ((from is not null && start is null) || (to is not null && end is null))
        {
            return null;
        }

        var resolvedEnd = end ?? today;
        var resolvedStart = start ?? resolvedEnd.AddDays(-(AnalyticsLimits.DefaultRangeDays - 1));

        if (resolvedStart > resolvedEnd)
        {
            errors.Add(new(AnalyticsFilters.From, "'from' must be on or before 'to'."));
            return null;
        }

        if (resolvedEnd.DayNumber - resolvedStart.DayNumber + 1 > AnalyticsLimits.MaxRangeDays)
        {
            errors.Add(new(AnalyticsFilters.From,
                $"The range from {resolvedStart:yyyy-MM-dd} to {resolvedEnd:yyyy-MM-dd} is longer than {AnalyticsLimits.MaxRangeDays} days."));
            return null;
        }

        return new AnalyticsDateRange(resolvedStart, resolvedEnd);
    }

    public static int? ParseLimit(
        int? limit,
        int defaultLimit,
        int maxLimit,
        ICollection<KeyValuePair<string, string>> errors)
    {
        var value = limit ?? defaultLimit;

        if (value < 1 || value > maxLimit)
        {
            errors.Add(new("limit", $"'limit' must be between 1 and {maxLimit}."));
            return null;
        }

        return value;
    }

    /// <summary>Accepts the enum name (case, '_' and '-' ignored) or its defined number.</summary>
    public static TEnum? ParseEnum<TEnum>(
        string parameter,
        string? raw,
        ICollection<KeyValuePair<string, string>> errors)
        where TEnum : struct, Enum
    {
        if (raw is null)
        {
            return null;
        }

        var normalized = raw.Trim().Replace("_", string.Empty).Replace("-", string.Empty).Replace(" ", string.Empty);

        if (int.TryParse(normalized, NumberStyles.None, CultureInfo.InvariantCulture, out var number)
            && Enum.IsDefined(typeof(TEnum), number))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), number);
        }

        foreach (var candidate in Enum.GetValues<TEnum>())
        {
            if (string.Equals(candidate.ToString(), normalized, StringComparison.OrdinalIgnoreCase))
            {
                return candidate;
            }
        }

        errors.Add(new(parameter,
            $"'{Echo(raw)}' is not a valid {parameter}. Allowed: {string.Join(", ", Enum.GetNames<TEnum>())}."));
        return null;
    }

    public static Guid? ParseGuid(
        string parameter,
        string? raw,
        ICollection<KeyValuePair<string, string>> errors)
    {
        if (raw is null)
        {
            return null;
        }

        if (Guid.TryParse(raw.Trim(), out var value))
        {
            return value;
        }

        errors.Add(new(parameter, $"'{Echo(raw)}' is not a valid GUID."));
        return null;
    }

    /// <summary>Blank query-string values count as absent.</summary>
    public static string? Clean(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    /// <summary>Input echoed in error messages is shortened so a 400 never reflects large payloads.</summary>
    public static string Echo(string value)
    {
        var trimmed = value.Trim();
        return trimmed.Length <= MaxEchoLength ? trimmed : trimmed[..MaxEchoLength] + "…";
    }

    public static IDictionary<string, string[]> ToErrorDictionary(IEnumerable<KeyValuePair<string, string>> errors)
        => errors
            .GroupBy(error => error.Key)
            .ToDictionary(group => group.Key, group => group.Select(error => error.Value).Distinct().ToArray());

    private static DateOnly? ParseDate(
        string parameter,
        string? raw,
        ICollection<KeyValuePair<string, string>> errors)
    {
        if (raw is null)
        {
            return null;
        }

        if (DateOnly.TryParseExact(raw.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var value))
        {
            return value;
        }

        errors.Add(new(parameter, $"'{Echo(raw)}' is not an ISO date (yyyy-MM-dd)."));
        return null;
    }
}
