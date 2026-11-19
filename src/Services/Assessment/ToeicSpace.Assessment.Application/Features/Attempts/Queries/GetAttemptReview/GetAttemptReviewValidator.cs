namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptReview;

public sealed class GetAttemptReviewValidator : AbstractValidator<GetAttemptReviewQuery>
{
    public GetAttemptReviewValidator()
    {
        RuleFor(query => query.AttemptId).NotEmpty();
    }
}
