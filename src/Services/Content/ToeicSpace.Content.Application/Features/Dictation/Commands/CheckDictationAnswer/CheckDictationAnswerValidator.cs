using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.Dictation.Commands.CheckDictationAnswer;

public sealed class CheckDictationAnswerValidator : AbstractValidator<CheckDictationAnswerCommand>
{
    public CheckDictationAnswerValidator()
    {
        RuleFor(x => x.ItemId).GreaterThan(0).WithMessage("Thiếu itemId.");
        RuleFor(x => x.UserText).MaximumLength(DictationGrader.MaxUserText * 2);
        RuleFor(x => x.Voice).MaximumLength(30);
        RuleFor(x => x.LearnerId).Must(x => x != Guid.Empty).When(x => x.LearnerId is not null);
    }
}
