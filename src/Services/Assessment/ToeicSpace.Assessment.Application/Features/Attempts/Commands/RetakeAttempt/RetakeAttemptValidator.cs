namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.RetakeAttempt;

public sealed class RetakeAttemptValidator : AbstractValidator<RetakeAttemptCommand>
{
    public RetakeAttemptValidator()
    {
        RuleFor(command => command.AttemptId).NotEmpty();
        RuleFor(command => command.Scope).NotNull().IsInEnum();
    }
}
