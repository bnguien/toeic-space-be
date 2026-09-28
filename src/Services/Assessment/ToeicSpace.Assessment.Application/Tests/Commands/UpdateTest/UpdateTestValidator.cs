using ToeicSpace.Assessment.Application.Tests.Common;

namespace ToeicSpace.Assessment.Application.Tests.Commands.UpdateTest;

public sealed class UpdateTestValidator : AbstractValidator<UpdateTestCommand>
{
    public UpdateTestValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        Include(new TestContentValidator());
    }
}
