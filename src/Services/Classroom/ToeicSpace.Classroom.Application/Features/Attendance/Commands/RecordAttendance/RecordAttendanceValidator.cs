namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.RecordAttendance;

public sealed class RecordAttendanceValidator : AbstractValidator<RecordAttendanceCommand>
{
    public RecordAttendanceValidator()
    {
        RuleFor(x => x.Session).NotEmpty();
        RuleFor(x => x.Actor).NotEmpty();
        RuleFor(x => x.Entry).NotNull();
        When(x => x.Entry is not null, () =>
        {
            RuleFor(x => x.Entry.StudentId).NotEmpty();
            RuleFor(x => x.Entry.Status).IsInEnum();
            RuleFor(x => x.Entry.Note).MaximumLength(1000);
        });
    }
}