using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Analytics.Common;

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
        catch (Exception exception) when (exception is not ClassRuleException
            && timeout.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            throw new ClassRuleException(
                AnalyticsErrorCodes.Timeout,
                $"The analytics query took longer than {AnalyticsLimits.QueryTimeout.TotalSeconds:0} seconds. Narrow the date range or add filters.",
                503,
                exception);
        }
    }

    /// <summary>
    /// Readers load at most <see cref="AnalyticsLimits.MaxFactRows"/> + 1 rows; one row more than the cap
    /// means the selection is too large and the request is refused.
    /// </summary>
    public static IReadOnlyList<T> EnsureWithinLimit<T>(IReadOnlyList<T> rows)
    {
        if (rows.Count > AnalyticsLimits.MaxFactRows)
        {
            throw new ClassRuleException(
                AnalyticsErrorCodes.RangeTooLarge,
                $"The selection holds more than {AnalyticsLimits.MaxFactRows} rows. Narrow the date range or add filters.");
        }

        return rows;
    }
}
