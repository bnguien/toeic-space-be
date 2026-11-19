namespace ToeicSpace.Content.Application.Features.DictationLearner.Queries.GetDictationLearnerState;

public sealed class GetDictationLearnerStateValidator : AbstractValidator<GetDictationLearnerStateQuery>
{
    public GetDictationLearnerStateValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
    }
}
