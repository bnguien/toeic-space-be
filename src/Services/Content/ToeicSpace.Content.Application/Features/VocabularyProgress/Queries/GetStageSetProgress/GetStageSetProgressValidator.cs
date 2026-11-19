using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStageSetProgress;

public sealed class GetStageSetProgressValidator : AbstractValidator<GetStageSetProgressQuery>
{
    public GetStageSetProgressValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.StageId).NotEmpty();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
