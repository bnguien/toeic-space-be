namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SetKnownWord;

public sealed class SetKnownWordValidator : AbstractValidator<SetKnownWordCommand>
{
    public SetKnownWordValidator()
    {
        RuleFor(x => x.Topic).Matches(@"^\d{2}$").WithMessage("topic must be two digits.");
        RuleFor(x => x.Word).NotEmpty().MaximumLength(80);
    }
}
