using ToeicSpace.Identity.Application.Features.Analytics.Common;
using ToeicSpace.Identity.Application.Interfaces.Persistence;
using static ToeicSpace.Identity.Application.Features.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;

/// <summary>Sign-in metrics read from refresh tokens. Groups with fewer than 5 distinct accounts are suppressed.</summary>
public static class SignInMetrics
{
    public static Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IIdentityDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
        => request.Metric.Name switch
        {
            Logins => ComputeLoginsAsync(context, request, cancellationToken),
            LoginUsers => ComputeLoginUsersAsync(context, request, cancellationToken),
            _ => throw new InvalidOperationException($"Metric '{request.Metric.Name}' is not a sign-in metric.")
        };

    private static async Task<IReadOnlyList<AnalyticsGroup>> ComputeLoginsAsync(
        IIdentityDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var signIns = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.SignIns(context, request),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(signIns, request.Dimensions,
            (dimension, signIn) => dimension == AnalyticsDimensions.Role
                ? signIn.Role
                : AnalyticsGrouping.TimeBucket(dimension, signIn.StartedAt),
            group => (group.Count, group.Count, group.Select(signIn => signIn.UserId).Distinct().Count()));
    }

    private static async Task<IReadOnlyList<AnalyticsGroup>> ComputeLoginUsersAsync(
        IIdentityDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var days = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.ActiveDays(context, request),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(days, request.Dimensions,
            (dimension, day) => dimension == AnalyticsDimensions.Role
                ? day.Role
                : AnalyticsGrouping.TimeBucket(dimension, day.Day),
            group =>
            {
                var accounts = group.Select(day => day.UserId).Distinct().Count();
                return (accounts, accounts, accounts);
            });
    }
}
