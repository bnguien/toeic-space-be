namespace ToeicSpace.Assessment.Application.Passages.Commands.DeletePassage;

public sealed class DeletePassageValidator : AbstractValidator<DeletePassageCommand>
{
    public DeletePassageValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
