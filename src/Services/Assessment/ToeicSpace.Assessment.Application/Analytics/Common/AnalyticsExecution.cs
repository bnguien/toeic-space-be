namespace ToeicSpace.Assessment.Application.Analytics.Common;

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
        catch (Exception exception) when (exception is not AppException
            && timeout.IsCancellationRequested
            && !cancellationToken.IsCancellationRequested)
        {
            throw new AppException(
                ErrorType.ServiceUnavailable,
                $"The analytics query took longer than {AnalyticsLimits.QueryTimeout.TotalSeconds:0} seconds. Narrow the date range or add filters.",
                ErrorCodes.AnalyticsTimeout);
        }
    }

    /// <summary>Loads at most <see cref="AnalyticsLimits.MaxFactRows"/> rows, or refuses the request.</summary>
    public static async Task<List<T>> LoadAsync<T>(
        IQueryable<T> query,
        CancellationToken cancellationToken)
    {
        var rows = await query
            .Take(AnalyticsLimits.MaxFactRows + 1)
            .ToListAsync(cancellationToken);

        if (rows.Count > AnalyticsLimits.MaxFactRows)
        {
            throw AppException.Validation(
                $"The selection holds more than {AnalyticsLimits.MaxFactRows} rows. Narrow the date range or add filters.",
                ErrorCodes.AnalyticsRangeTooLarge);
        }

        return rows;
    }
}
