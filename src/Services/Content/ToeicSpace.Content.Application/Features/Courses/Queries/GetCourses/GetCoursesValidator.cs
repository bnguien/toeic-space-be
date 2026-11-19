namespace ToeicSpace.Content.Application.Features.Courses.Queries.GetCourses;

public sealed class GetCoursesValidator : AbstractValidator<GetCoursesQuery>
{
    public GetCoursesValidator()
    {
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Level).IsInEnum().When(x => x.Level.HasValue);
        RuleFor(x => x.ExamType).IsInEnum().When(x => x.ExamType.HasValue);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.Page <= 0 || x.PageSize <= 0 || x.Page <= int.MaxValue / x.PageSize)
            .WithMessage("Page and pageSize exceed the supported range.");
    }
}
