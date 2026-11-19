namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.UnassignTeacher;

public sealed class UnassignTeacherValidator : AbstractValidator<UnassignTeacherCommand>
{
    public UnassignTeacherValidator() => RuleFor(x => x.ClassId).NotEmpty();
}
