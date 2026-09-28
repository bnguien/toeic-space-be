using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSet;

public sealed class UpdatePracticeSetValidator : AbstractValidator<UpdatePracticeSetCommand>
{
    public UpdatePracticeSetValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        Include(new PracticeSetContentValidator());
    }
}
