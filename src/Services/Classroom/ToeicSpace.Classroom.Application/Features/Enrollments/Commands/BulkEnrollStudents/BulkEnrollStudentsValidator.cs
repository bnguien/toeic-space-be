namespace ToeicSpace.Classroom.Application.Features.Enrollments.Commands.BulkEnrollStudents;

public sealed class BulkEnrollStudentsValidator : AbstractValidator<BulkEnrollStudentsCommand>
{
    public BulkEnrollStudentsValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.AdminId).NotEmpty();
        RuleFor(x => x.StudentIds).NotNull().Must(x => x is { Count: >= 1 and <= 1000 })
            .WithMessage("Provide 1-1000 student IDs.");
        RuleForEach(x => x.StudentIds).NotEmpty();
    }
}
