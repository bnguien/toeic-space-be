using ToeicSpace.Identity.Application.Features.Analytics.Common;
using ToeicSpace.Identity.Application.Interfaces.Persistence;

namespace ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;

/// <summary>Counts of accounts: registrations, snapshots, external sign-in links and inactive accounts.</summary>
public static class AccountMetrics
{
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeRegistrationsAsync(
        IIdentityDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.Registrations(context, request),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (dimension, partial) => dimension == AnalyticsDimensions.Role
                ? partial.Role
                : AnalyticsGrouping.TimeBucket(dimension, partial.Day),
            group => Count(group.Sum(partial => partial.Accounts)));
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAccountsAsync(
        IIdentityDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.Accounts(context, request),
            cancellationToken);

        return CountAccounts(partials, request);
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeInactiveAsync(
        IIdentityDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.InactiveAccounts(context, request, AnalyticsLimits.InactivityDays),
            cancellationToken);

        return CountAccounts(partials, request);
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeOAuthLinksAsync(
        IIdentityDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.OAuthLinks(context, request),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (_, partial) => partial.Provider,
            group => Count(group.Sum(partial => partial.Accounts)));
    }

    private static IReadOnlyList<AnalyticsGroup> CountAccounts(
        IReadOnlyList<AccountPartial> partials,
        AnalyticsRequest request)
        => AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (dimension, partial) => dimension switch
            {
                AnalyticsDimensions.Role => partial.Role,
                AnalyticsDimensions.Status => partial.Status,
                _ => null
            },
            group => Count(group.Sum(partial => partial.Accounts)));

    private static (double? Value, int Count, int Accounts) Count(int accounts)
        => (accounts, accounts, accounts);
}
