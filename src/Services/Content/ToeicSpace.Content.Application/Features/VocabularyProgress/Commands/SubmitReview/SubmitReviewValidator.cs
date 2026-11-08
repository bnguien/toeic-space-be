using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;

public sealed class SubmitReviewValidator : AbstractValidator<SubmitReviewCommand>
{
    public SubmitReviewValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.WordId).NotEmpty();
        RuleFor(x => x.ClientEventId).NotEmpty();
        RuleFor(x => x.Mode).IsInEnum();
        RuleFor(x => x.Grade).IsInEnum();
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
