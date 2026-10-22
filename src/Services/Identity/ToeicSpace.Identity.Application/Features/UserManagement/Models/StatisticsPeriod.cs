namespace ToeicSpace.Identity.Application.Features.UserManagement.Models;

/// <summary>
/// Time ranges offered by the admin trend charts. Each one is cut into buckets small enough to
/// show change without producing hundreds of points.
/// </summary>
public sealed record StatisticsPeriod(string Key, int BucketCount, int BucketHours)
{
    public static readonly IReadOnlyList<StatisticsPeriod> All =
    [
        new("24h", 24, 1),
        new("3d", 24, 3),
        new("7d", 7, 24),
        new("30d", 30, 24),
        new("90d", 13, 24 * 7)
    ];

    public const string DefaultKey = "7d";

    public static StatisticsPeriod? Find(string? key)
        => All.FirstOrDefault(period =>
            string.Equals(period.Key, key?.Trim(), StringComparison.OrdinalIgnoreCase));

    public TimeSpan BucketSize => TimeSpan.FromHours(BucketHours);

    public TimeSpan Length => BucketSize * BucketCount;

    /// <summary>Daily and weekly buckets start at local midnight, hourly ones on the hour.</summary>
    public bool AlignsToDays => BucketHours >= 24;
}
