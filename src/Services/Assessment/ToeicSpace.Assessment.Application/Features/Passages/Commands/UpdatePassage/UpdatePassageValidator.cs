using ToeicSpace.Assessment.Application.Common.Validation;


namespace ToeicSpace.Assessment.Application.Features.Passages.Commands.UpdatePassage;

public sealed class UpdatePassageValidator : AbstractValidator<UpdatePassageCommand>
{
    public UpdatePassageValidator()
    {
        RuleFor(command => command.Id)
            .NotEmpty();

        Include(new PassageContentValidator());
    }
}
