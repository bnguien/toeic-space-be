using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.Passages.Commands.CreatePassage;

public sealed class CreatePassageValidator : AbstractValidator<CreatePassageCommand>
{
    public CreatePassageValidator()
    {
        Include(new PassageContentValidator());
    }
}
