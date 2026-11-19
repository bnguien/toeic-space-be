using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;

public sealed class GetLearnerSummaryValidator : AbstractValidator<GetLearnerSummaryQuery>
{
    public GetLearnerSummaryValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
