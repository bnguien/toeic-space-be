namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.SubmitAttempt;

public sealed class SubmitAttemptValidator : AbstractValidator<SubmitAttemptCommand>
{
    public SubmitAttemptValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
    }
}
