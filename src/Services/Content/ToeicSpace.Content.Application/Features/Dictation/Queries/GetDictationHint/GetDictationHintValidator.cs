using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationHint;

public sealed class GetDictationHintValidator : AbstractValidator<GetDictationHintQuery>
{
    public GetDictationHintValidator()
    {
        RuleFor(x => x.ItemId).GreaterThan(0).WithMessage("Thiếu itemId.");
        RuleFor(x => x.Mode).IsInEnum().When(x => x.Mode is not null);
        RuleFor(x => x.UserText).MaximumLength(DictationGrader.MaxUserText * 2);
    }
}
