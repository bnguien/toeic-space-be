namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyEnrolledClasses;

public sealed class GetMyEnrolledClassesValidator : AbstractValidator<GetMyEnrolledClassesQuery>
{
    public GetMyEnrolledClassesValidator()
    {
        RuleFor(x => x.StudentId).NotEmpty();
        RuleFor(x => x.Status).IsInEnum().When(x => x.Status.HasValue);
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.Page <= 0 || x.PageSize <= 0 || x.Page <= int.MaxValue / x.PageSize)
            .WithMessage("Page and pageSize exceed the supported range.");
    }
}
