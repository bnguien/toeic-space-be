namespace ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;

public sealed class UpdatePracticeSetStatusValidator : AbstractValidator<UpdatePracticeSetStatusCommand>
{
    public UpdatePracticeSetStatusValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Status)
            .Must(status => status is ContentStatus.Active or ContentStatus.Archived)
            .WithMessage("Status must be Active or Archived.");
    }
}
