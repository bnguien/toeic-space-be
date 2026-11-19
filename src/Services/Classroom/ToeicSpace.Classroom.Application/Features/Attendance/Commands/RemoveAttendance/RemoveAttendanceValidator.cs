namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.RemoveAttendance;

public sealed class RemoveAttendanceValidator : AbstractValidator<RemoveAttendanceCommand>
{
    public RemoveAttendanceValidator()
    {
        RuleFor(x => x.Session).NotEmpty();
        RuleFor(x => x.Actor).NotEmpty();
        RuleFor(x => x.Student).NotEmpty();
    }
}