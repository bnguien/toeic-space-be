using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.Application.Analytics.Common;

public static class AnalyticsExecution
{
    /// <summary>
    /// Runs an analytics read with the <see cref="AnalyticsLimits.QueryTimeout"/> budget. Database commands are
    /// cancelled when it runs out and the caller gets a 503 instead of a long-running request.
    /// </summary>
    public static async Task<T> WithTimeoutAsync<T>(
        Func<CancellationToken, Task<T>> read,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(AnalyticsLimits.QueryTimeout);

        try
        {
            return await read(timeout.Token);
        }
        catch (Exception exception) when (exception is not ContentRuleException
            && timeout.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            throw AnalyticsErrors.Timeout(AnalyticsLimits.QueryTimeout);
        }
    }

    /// <summary>
    /// Loads at most <see cref="AnalyticsLimits.MaxFactRows"/> rows, or refuses the request. The loader receives
    /// the number of rows to take (one more than allowed, so an oversized selection is detected).
    /// </summary>
    public static async Task<IReadOnlyList<T>> LoadAsync<T>(
        Func<int, CancellationToken, Task<IReadOnlyList<T>>> load,
        CancellationToken cancellationToken)
    {
        var rows = await load(AnalyticsLimits.MaxFactRows + 1, cancellationToken);

        if (rows.Count > AnalyticsLimits.MaxFactRows)
        {
            throw AnalyticsErrors.RangeTooLarge(AnalyticsLimits.MaxFactRows);
        }

        return rows;
    }
}
