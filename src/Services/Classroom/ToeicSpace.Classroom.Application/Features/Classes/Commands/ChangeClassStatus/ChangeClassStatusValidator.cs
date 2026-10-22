namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.ChangeClassStatus;

public sealed class ChangeClassStatusValidator : AbstractValidator<ChangeClassStatusCommand>
{
    public ChangeClassStatusValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum();
    }
}
