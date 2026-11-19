namespace ToeicSpace.Classroom.Application.Features.Classes.Commands.AssignTeacher;

public sealed class AssignTeacherValidator : AbstractValidator<AssignTeacherCommand>
{
    public AssignTeacherValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.TeacherId).NotEmpty();
    }
}