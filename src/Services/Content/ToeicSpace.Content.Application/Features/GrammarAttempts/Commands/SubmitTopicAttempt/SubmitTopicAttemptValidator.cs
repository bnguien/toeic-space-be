using ToeicSpace.Content.Application.Models.Grammar;

namespace ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitTopicAttempt;

public sealed class SubmitTopicAttemptValidator : AbstractValidator<SubmitTopicAttemptCommand>
{
    public SubmitTopicAttemptValidator()
    {
        RuleFor(x => x.Topic).Matches(@"^\d{2}$").WithMessage("topic must be two digits.");
        RuleFor(x => x.Block).NotEmpty().Must(b => GrammarBlocks.TopicBlock.IsMatch(b ?? string.Empty))
            .WithMessage("block must be qc-<unit> or mt.");
    }
}
