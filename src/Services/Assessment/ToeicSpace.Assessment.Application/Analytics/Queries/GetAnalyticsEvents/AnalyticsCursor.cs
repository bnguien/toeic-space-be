using System.Buffers.Text;
using System.Globalization;
using System.Text;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;

/// <summary>
/// Keyset position of the last event of a page: attempt start time and the row ID. Opaque to callers
/// (base64url) and bound to one event type.
/// </summary>
public sealed record AnalyticsCursor(string Type, DateTime StartTime, Guid Id)
{
    public string Encode()
        => Base64Url.EncodeToString(Encoding.UTF8.GetBytes(
            $"{Type}|{StartTime.Ticks.ToString(CultureInfo.InvariantCulture)}|{Id:N}"));

    public static AnalyticsCursor? TryDecode(string value)
    {
        try
        {
            var parts = Encoding.UTF8.GetString(Base64Url.DecodeFromChars(value.Trim())).Split('|');

            return parts.Length == 3
                && AnalyticsEventTypes.All.Contains(parts[0])
                && long.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var ticks)
                && ticks >= DateTime.MinValue.Ticks && ticks <= DateTime.MaxValue.Ticks
                && Guid.TryParseExact(parts[2], "N", out var id)
                    ? new AnalyticsCursor(parts[0], new DateTime(ticks, DateTimeKind.Utc), id)
                    : null;
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
