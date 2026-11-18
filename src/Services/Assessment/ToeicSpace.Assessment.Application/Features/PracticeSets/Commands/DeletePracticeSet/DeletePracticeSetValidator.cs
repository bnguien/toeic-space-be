namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.DeletePracticeSet;

public sealed class DeletePracticeSetValidator : AbstractValidator<DeletePracticeSetCommand>
{
    public DeletePracticeSetValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
