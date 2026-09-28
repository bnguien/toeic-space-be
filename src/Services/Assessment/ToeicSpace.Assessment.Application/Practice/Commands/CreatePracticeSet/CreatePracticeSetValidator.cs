using ToeicSpace.Assessment.Application.Practice.Common;

namespace ToeicSpace.Assessment.Application.Practice.Commands.CreatePracticeSet;

public sealed class CreatePracticeSetValidator : AbstractValidator<CreatePracticeSetCommand>
{
    public CreatePracticeSetValidator()
    {
        Include(new PracticeSetContentValidator());
    }
}
