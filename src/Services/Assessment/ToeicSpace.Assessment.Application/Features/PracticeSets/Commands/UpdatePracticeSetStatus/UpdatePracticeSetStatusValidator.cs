namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.UpdatePracticeSetStatus;

public sealed class UpdatePracticeSetStatusValidator : AbstractValidator<UpdatePracticeSetStatusCommand>
{
    public UpdatePracticeSetStatusValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        RuleFor(command => command.Status)
            .IsInEnum();
    }
}
