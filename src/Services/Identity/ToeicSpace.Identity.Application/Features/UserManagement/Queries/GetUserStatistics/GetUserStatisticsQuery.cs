using ToeicSpace.Identity.Application.Features.UserManagement.Results;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserStatistics;

/// <summary>
/// Counts for one role (the Students, Teachers and Admins pages) or for every account, plus the
/// trend of the period (24h, 3d, 7d, 30d or 90d).
/// TimeZoneOffsetMinutes (UTC+7 = 420) decides where days and hours start.
/// </summary>
public sealed record GetUserStatisticsQuery(
    string? Role,
    int TimeZoneOffsetMinutes = 0,
    string? Period = null) : IRequest<UserStatisticsResult>;
