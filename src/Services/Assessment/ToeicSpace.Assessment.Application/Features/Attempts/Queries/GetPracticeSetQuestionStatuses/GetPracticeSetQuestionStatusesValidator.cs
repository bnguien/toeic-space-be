namespace ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetQuestionStatuses;

public sealed class GetPracticeSetQuestionStatusesValidator : AbstractValidator<GetPracticeSetQuestionStatusesQuery>
{
    public GetPracticeSetQuestionStatusesValidator()
    {
        RuleFor(query => query.PracticeSetId).NotEmpty();
    }
}
