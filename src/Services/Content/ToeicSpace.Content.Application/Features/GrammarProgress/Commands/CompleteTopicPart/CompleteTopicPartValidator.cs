namespace ToeicSpace.Content.Application.Features.GrammarProgress.Commands.CompleteTopicPart;

public sealed class CompleteTopicPartValidator : AbstractValidator<CompleteTopicPartCommand>
{
    public CompleteTopicPartValidator()
    {
        RuleFor(x => x.Part).Must(p => p is CompleteTopicPartCommand.Theory or CompleteTopicPartCommand.Vocab)
            .WithMessage("part must be theory or vocab.");
    }
}
