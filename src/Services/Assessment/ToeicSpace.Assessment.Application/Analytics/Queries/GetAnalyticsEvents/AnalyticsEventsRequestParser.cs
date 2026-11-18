using ToeicSpace.Assessment.Application.Analytics.Common;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;

public sealed record AnalyticsEventsRequest(
    string Type,
    AnalyticsDateRange Range,
    AnalyticsCursor? Cursor,
    int Limit);

public static class AnalyticsEventsRequestParser
{
    private const int MaxCursorLength = 200;

    /// <summary>Returns null and fills <paramref name="errors"/> when the query is not valid.</summary>
    public static AnalyticsEventsRequest? Parse(
        GetAnalyticsEventsQuery query,
        DateOnly today,
        ICollection<KeyValuePair<string, string>> errors)
    {
        var errorCount = errors.Count;
        var allowed = string.Join(", ", AnalyticsEventTypes.All);
        var type = AnalyticsValues.Clean(query.Type)?.ToLowerInvariant();

        if (type is null)
        {
            errors.Add(new("type", $"'type' is required. Allowed: {allowed}."));
        }
        else if (!AnalyticsEventTypes.All.Contains(type))
        {
            errors.Add(new("type", $"Unknown event type '{AnalyticsValues.Echo(type)}'. Allowed: {allowed}."));
        }

        var range = AnalyticsValues.ParseRange(
            AnalyticsValues.Clean(query.From),
            AnalyticsValues.Clean(query.To),
            today,
            errors);

        AnalyticsCursor? cursor = null;
        var rawCursor = AnalyticsValues.Clean(query.Cursor);

        if (rawCursor is not null)
        {
            cursor = rawCursor.Length <= MaxCursorLength ? AnalyticsCursor.TryDecode(rawCursor) : null;

            if (cursor is null)
            {
                errors.Add(new("cursor", "'cursor' is not valid. Pass back nextCursor of the previous page unchanged."));
            }
            else if (type is not null && cursor.Type != type)
            {
                errors.Add(new("cursor", $"'cursor' belongs to '{cursor.Type}' events, not '{AnalyticsValues.Echo(type)}'."));
            }
        }

        var limit = AnalyticsValues.ParseLimit(
            query.Limit,
            AnalyticsLimits.DefaultEventLimit,
            AnalyticsLimits.MaxEventLimit,
            errors);

        return errors.Count > errorCount || type is null || range is null || limit is null
            ? null
            : new AnalyticsEventsRequest(type, range, cursor, limit.Value);
    }
}
