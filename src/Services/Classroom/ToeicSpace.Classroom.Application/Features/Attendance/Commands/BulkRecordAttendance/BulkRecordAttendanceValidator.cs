namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.BulkRecordAttendance;

public sealed class BulkRecordAttendanceValidator : AbstractValidator<BulkRecordAttendanceCommand>
{
    public BulkRecordAttendanceValidator()
    {
        RuleFor(x => x.Session).NotEmpty();
        RuleFor(x => x.Actor).NotEmpty();
        RuleFor(x => x.Items).NotNull().Must(x =>
            x is not null && x.Count <= 100 && x.Select(i => i.StudentId).Distinct().Count() == x.Count);
        RuleForEach(x => x.Items).ChildRules(entry =>
        {
            entry.RuleFor(x => x.StudentId).NotEmpty();
            entry.RuleFor(x => x.Status).IsInEnum();
            entry.RuleFor(x => x.Note).MaximumLength(1000);
        });
    }
}