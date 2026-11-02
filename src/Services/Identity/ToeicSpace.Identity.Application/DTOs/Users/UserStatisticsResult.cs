namespace ToeicSpace.Identity.Application.DTOs.Users;

/// <summary>
/// Counts for the list header. Every count except Deleted excludes soft-deleted users.
/// Trend covers the requested period in the caller's time zone.
/// </summary>
public sealed record UserStatisticsResult(
    int Total,
    int Active,
    int Suspended,
    int Unverified,
    int Deleted,
    int CreatedLast30Days,
    UserTrendResult Trend);
