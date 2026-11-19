namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActiveTestAttempt;

public sealed class GetActiveTestAttemptValidator : AbstractValidator<GetActiveTestAttemptQuery>
{
    public GetActiveTestAttemptValidator()
    {
        RuleFor(query => query.TestId).NotEmpty();
    }
}
