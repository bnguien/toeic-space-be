namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.ResumeAttempt;

public sealed class ResumeAttemptValidator : AbstractValidator<ResumeAttemptCommand>
{
    public ResumeAttemptValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
    }
}
