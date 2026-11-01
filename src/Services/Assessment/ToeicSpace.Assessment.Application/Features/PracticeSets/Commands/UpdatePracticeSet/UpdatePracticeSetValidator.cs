using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.UpdatePracticeSet;

public sealed class UpdatePracticeSetValidator : AbstractValidator<UpdatePracticeSetCommand>
{
    public UpdatePracticeSetValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        Include(new PracticeSetContentValidator());
    }
}
