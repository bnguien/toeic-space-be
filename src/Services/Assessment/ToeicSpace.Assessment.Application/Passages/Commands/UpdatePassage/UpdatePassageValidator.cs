using ToeicSpace.Assessment.Application.Passages.Common;

namespace ToeicSpace.Assessment.Application.Passages.Commands.UpdatePassage;

public sealed class UpdatePassageValidator : AbstractValidator<UpdatePassageCommand>
{
    public UpdatePassageValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        Include(new PassageContentValidator());
    }
}
