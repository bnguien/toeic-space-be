using ToeicSpace.Identity.Application.Features.UserManagement.Common;
using ToeicSpace.Identity.Application.Features.UserManagement.Models;
using ToeicSpace.Identity.Application.Features.UserManagement.Results;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using ToeicSpace.Identity.Domain.Enums;

namespace ToeicSpace.Identity.Application.Features.UserManagement.Queries.GetUserStatistics;

public sealed class GetUserStatisticsHandler
    : IRequestHandler<GetUserStatisticsQuery, UserStatisticsResult>
{
    private const int RecentDays = 30;

    private readonly IUserRepository _userRepository;
    private readonly TimeProvider _timeProvider;

    public GetUserStatisticsHandler(
        IUserRepository userRepository,
        TimeProvider timeProvider)
    {
        _userRepository = userRepository;
        _timeProvider = timeProvider;
    }

    public async Task<UserStatisticsResult> Handle(
        GetUserStatisticsQuery request,
        CancellationToken cancellationToken)
    {
        UserRole? role = UserAdministrationRules.TryParseRole(request.Role, out var parsedRole)
            ? parsedRole
            : null;
        var period = StatisticsPeriod.Find(request.Period)
            ?? StatisticsPeriod.Find(StatisticsPeriod.DefaultKey)!;
        var offset = TimeSpan.FromMinutes(request.TimeZoneOffsetMinutes);
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var counts = await _userRepository.GetCountsAsync(role, now.AddDays(-RecentDays), cancellationToken);
        var trend = await BuildTrendAsync(role, period, now, offset, cancellationToken);

        return new UserStatisticsResult(
            counts.Total,
            counts.Active,
            counts.Suspended,
            counts.Unverified,
            counts.Deleted,
            counts.CreatedSince,
            trend);
    }

    /// <summary>
    /// Buckets of the period ending with the current hour or day, in the caller's calendar,
    /// and the totals of the period just before it for comparison.
    /// </summary>
    private async Task<UserTrendResult> BuildTrendAsync(
        UserRole? role,
        StatisticsPeriod period,
        DateTime nowUtc,
        TimeSpan offset,
        CancellationToken cancellationToken)
    {
        var localNow = nowUtc + offset;
        var end = period.AlignsToDays
            ? localNow.Date.AddDays(1)
            : new DateTime(localNow.Year, localNow.Month, localNow.Day, localNow.Hour, 0, 0).AddHours(1);
        var start = end - period.Length;
        var previousStart = start - period.Length;
        var sinceUtc = previousStart - offset;

        var created = (await _userRepository.GetCreationTimesAsync(role, sinceUtc, cancellationToken))
            .Select(time => time + offset)
            .ToList();
        var signIns = (await _userRepository.GetSignInsAsync(role, sinceUtc, cancellationToken))
            .Select(signIn => signIn with { At = signIn.At + offset })
            .ToList();

        int? BucketOf(DateTime local)
        {
            if (local < start || local >= end) return null;
            return (int)((local - start).Ticks / period.BucketSize.Ticks);
        }

        var registrations = new int[period.BucketCount];
        foreach (var bucket in created.Select(BucketOf).OfType<int>())
        {
            registrations[bucket]++;
        }

        var activeUsers = signIns
            .Select(signIn => (Bucket: BucketOf(signIn.At), signIn.UserId))
            .Where(item => item.Bucket is not null)
            .GroupBy(item => item.Bucket!.Value)
            .ToDictionary(group => group.Key, group => group.Select(item => item.UserId).Distinct().Count());

        var buckets = Enumerable.Range(0, period.BucketCount)
            .Select(index => new TrendBucketResult(
                start + period.BucketSize * index,
                registrations[index],
                activeUsers.GetValueOrDefault(index)))
            .ToList();

        bool InCurrent(DateTime local) => local >= start && local < end;
        bool InPrevious(DateTime local) => local >= previousStart && local < start;

        return new UserTrendResult(
            period.Key,
            period.AlignsToDays ? "day" : "hour",
            period.BucketHours,
            buckets,
            created.Count(InCurrent),
            created.Count(InPrevious),
            signIns.Where(signIn => InCurrent(signIn.At)).Select(signIn => signIn.UserId).Distinct().Count(),
            signIns.Where(signIn => InPrevious(signIn.At)).Select(signIn => signIn.UserId).Distinct().Count());
    }
}
