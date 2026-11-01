namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.ImportStudents;

public sealed class ImportStudentsValidator : AbstractValidator<ImportStudentsCommand>
{
    public ImportStudentsValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.Rows).NotNull().Must(x => x is { Count: > 0 and <= 1000 })
            .WithMessage("File must contain 1-1000 data rows.");
    }
}
