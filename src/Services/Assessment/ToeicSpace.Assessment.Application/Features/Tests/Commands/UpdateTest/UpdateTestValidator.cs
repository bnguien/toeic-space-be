using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.Tests.Commands.UpdateTest;

public sealed class UpdateTestValidator : AbstractValidator<UpdateTestCommand>
{
    public UpdateTestValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        Include(new TestContentValidator());
    }
}
