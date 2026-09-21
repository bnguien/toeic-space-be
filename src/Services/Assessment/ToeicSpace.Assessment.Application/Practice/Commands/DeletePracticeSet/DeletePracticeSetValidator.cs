namespace ToeicSpace.Assessment.Application.Practice.Commands.DeletePracticeSet;

public sealed class DeletePracticeSetValidator : AbstractValidator<DeletePracticeSetCommand>
{
    public DeletePracticeSetValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
