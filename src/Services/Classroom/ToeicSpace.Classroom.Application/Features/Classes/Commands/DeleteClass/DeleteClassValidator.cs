namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.DeleteClass;

public sealed class DeleteClassValidator : AbstractValidator<DeleteClassCommand>
{
    public DeleteClassValidator() => RuleFor(x => x.ClassId).NotEmpty();
}
