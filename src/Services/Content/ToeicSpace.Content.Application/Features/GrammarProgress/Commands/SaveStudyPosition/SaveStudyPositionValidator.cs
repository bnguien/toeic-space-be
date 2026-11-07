namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.SaveStudyPosition;

public sealed class SaveStudyPositionValidator : AbstractValidator<SaveStudyPositionCommand>
{
    public SaveStudyPositionValidator()
    {
        RuleFor(x => x.Topic).Matches(@"^\d{2}$").WithMessage("topic must be two digits.");
        RuleFor(x => x.Part).Must(p => SaveStudyPositionCommand.Parts.Contains(p))
            .WithMessage("part must be theory, vocab or exercises.");
        RuleFor(x => x.Anchor).MaximumLength(120);
    }
}
