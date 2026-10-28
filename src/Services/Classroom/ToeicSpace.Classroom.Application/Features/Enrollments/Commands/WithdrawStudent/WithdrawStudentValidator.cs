namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.WithdrawStudent;

public sealed class WithdrawStudentValidator : AbstractValidator<WithdrawStudentCommand>
{
    public WithdrawStudentValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.EnrollmentId).NotEmpty();
    }
}
