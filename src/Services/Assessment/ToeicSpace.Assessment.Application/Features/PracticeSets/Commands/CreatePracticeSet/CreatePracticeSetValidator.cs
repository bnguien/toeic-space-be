using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.CreatePracticeSet;

public sealed class CreatePracticeSetValidator : AbstractValidator<CreatePracticeSetCommand>
{
    public CreatePracticeSetValidator()
    {
        Include(new PracticeSetContentValidator());
    }
}
