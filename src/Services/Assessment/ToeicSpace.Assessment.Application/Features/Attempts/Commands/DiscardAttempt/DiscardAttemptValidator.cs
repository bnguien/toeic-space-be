namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.DiscardAttempt;

public sealed class DiscardAttemptValidator : AbstractValidator<DiscardAttemptCommand>
{
    public DiscardAttemptValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
    }
}
