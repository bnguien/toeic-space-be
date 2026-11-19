namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempts;

public sealed class GetAttemptsValidator : AbstractValidator<GetAttemptsQuery>
{
    public GetAttemptsValidator()
    {
        RuleFor(query => query.Mode).IsInEnum();
        RuleFor(query => query.Kind).IsInEnum();
        RuleFor(query => query.Status).IsInEnum();
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}
