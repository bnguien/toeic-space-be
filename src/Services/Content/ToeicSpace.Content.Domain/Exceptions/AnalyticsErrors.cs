namespace ToeicSpace.Content.Domain.Exceptions;

/// <summary>Error codes of the read-only analytics endpoints (ProblemDetails <c>code</c>).</summary>
public static class AnalyticsErrors
{
    /// <summary>A 400 whose <c>errors</c> extension lists every failing parameter with the allowed values.</summary>
    public static ContentRuleException InvalidQuery(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation_error", errors.Values.SelectMany(messages => messages).FirstOrDefault() ?? "The analytics query is not valid.")
        {
            Extensions = new Dictionary<string, object?> { ["errors"] = errors }
        };

    public static ContentRuleException Timeout(TimeSpan budget) =>
        new("analytics_timeout",
            $"The analytics query took longer than {budget.TotalSeconds:0} seconds. Narrow the date range or add filters.", 503);

    public static ContentRuleException RangeTooLarge(int maxRows) =>
        new("analytics_range_too_large",
            $"The selection holds more than {maxRows} rows. Narrow the date range or add filters.");
}
