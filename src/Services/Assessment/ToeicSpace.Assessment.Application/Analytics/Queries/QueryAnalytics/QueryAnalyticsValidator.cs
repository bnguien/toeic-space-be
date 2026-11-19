using ToeicSpace.Assessment.Application.Analytics.Common;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

/// <summary>
/// Metric, dimensions and filters must come from the catalogue; anything else is a 400 listing the allowed values.
/// </summary>
public sealed class QueryAnalyticsValidator : AbstractValidator<QueryAnalyticsQuery>
{
    public QueryAnalyticsValidator(TimeProvider timeProvider)
    {
        // One custom rule: the parser checks the fields together (metric decides which dimensions and filters are allowed).
        RuleFor(query => query.Metric).Custom((_, context) =>
        {
            var query = context.InstanceToValidate;
            var errors = new List<KeyValuePair<string, string>>();
            AnalyticsRequestParser.Parse(query, AnalyticsValues.Today(timeProvider), errors);

            foreach (var (parameter, message) in errors)
            {
                context.AddFailure(parameter, message);
            }
        });
    }
}
