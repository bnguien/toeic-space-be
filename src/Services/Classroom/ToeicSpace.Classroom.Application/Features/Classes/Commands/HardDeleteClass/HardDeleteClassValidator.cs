namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.HardDeleteClass;

public sealed class HardDeleteClassValidator : AbstractValidator<HardDeleteClassCommand>
{
    public HardDeleteClassValidator() => RuleFor(x => x.ClassId).NotEmpty();
}
