namespace ToeicSpace.Assessment.Application.Tests.Commands.DeleteTest;

public sealed class DeleteTestValidator : AbstractValidator<DeleteTestCommand>
{
    public DeleteTestValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();
    }
}
