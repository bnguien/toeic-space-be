using ToeicSpace.Assessment.Application.Passages.Common;

namespace ToeicSpace.Assessment.Application.Passages.Commands.CreatePassage;

public sealed class CreatePassageValidator : AbstractValidator<CreatePassageCommand>
{
    public CreatePassageValidator()
    {
        Include(new PassageContentValidator());
    }
}
