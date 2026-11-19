namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.EnrollStudent;

public sealed class EnrollStudentValidator : AbstractValidator<EnrollStudentCommand>
{
    public EnrollStudentValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.StudentId).NotEmpty();
    }
}
