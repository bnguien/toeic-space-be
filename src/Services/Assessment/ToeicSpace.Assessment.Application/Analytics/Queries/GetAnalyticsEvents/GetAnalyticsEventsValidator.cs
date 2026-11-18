using ToeicSpace.Assessment.Application.Analytics.Common;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;

public sealed class GetAnalyticsEventsValidator : AbstractValidator<GetAnalyticsEventsQuery>
{
    public GetAnalyticsEventsValidator(TimeProvider timeProvider)
    {
        RuleFor(query => query.Type).Custom((_, context) =>
        {
            var errors = new List<KeyValuePair<string, string>>();
            AnalyticsEventsRequestParser.Parse(context.InstanceToValidate, AnalyticsValues.Today(timeProvider), errors);

            foreach (var (parameter, message) in errors)
            {
                context.AddFailure(parameter, message);
            }
        });
    }
}
