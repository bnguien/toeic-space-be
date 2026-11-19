namespace ToeicSpace.Classroom.Application.Features.Enrollments.Queries.GetTeacherClassStudents;

public sealed class GetTeacherClassStudentsValidator : AbstractValidator<GetTeacherClassStudentsQuery>
{
    public GetTeacherClassStudentsValidator()
    {
        RuleFor(x => x.ClassId).NotEmpty();
        RuleFor(x => x.TeacherId).NotEmpty();
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.Page <= 0 || x.PageSize <= 0 || x.Page <= int.MaxValue / x.PageSize)
            .WithMessage("Page and pageSize exceed the supported range.");
    }
}
