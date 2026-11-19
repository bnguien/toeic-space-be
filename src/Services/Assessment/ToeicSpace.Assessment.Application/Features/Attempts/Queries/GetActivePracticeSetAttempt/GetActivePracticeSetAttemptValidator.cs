namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActivePracticeSetAttempt;

public sealed class GetActivePracticeSetAttemptValidator : AbstractValidator<GetActivePracticeSetAttemptQuery>
{
    public GetActivePracticeSetAttemptValidator()
    {
        RuleFor(query => query.PracticeSetId).NotEmpty();
    }
}
