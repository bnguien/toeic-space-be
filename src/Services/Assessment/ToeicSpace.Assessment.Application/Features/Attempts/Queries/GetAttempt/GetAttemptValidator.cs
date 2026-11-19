namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempt;

public sealed class GetAttemptValidator : AbstractValidator<GetAttemptQuery>
{
    public GetAttemptValidator()
    {
        RuleFor(query => query.AttemptId).NotEmpty();
    }
}
