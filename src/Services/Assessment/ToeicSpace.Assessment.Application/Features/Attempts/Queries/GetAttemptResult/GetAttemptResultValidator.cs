namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptResult;

public sealed class GetAttemptResultValidator : AbstractValidator<GetAttemptResultQuery>
{
    public GetAttemptResultValidator()
    {
        RuleFor(query => query.AttemptId).NotEmpty();
    }
}
