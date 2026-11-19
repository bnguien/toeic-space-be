using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartPracticeSetAttempt;

public sealed class StartPracticeSetAttemptValidator : AbstractValidator<StartPracticeSetAttemptCommand>
{
    public StartPracticeSetAttemptValidator()
    {
        RuleFor(command => command.PracticeSetId).NotEmpty();
        RuleFor(command => command.ExpectedGeneration).GreaterThanOrEqualTo(0);
        RuleFor(command => command.Selection).IsInEnum();
        RuleFor(command => command.Count)
            .InclusiveBetween(PracticeSelectionPlanner.MinimumCount, PracticeSelectionPlanner.MaximumCount)
            .When(command => command.Count.HasValue);
    }
}
