namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetDeletedClasses;

public sealed class GetDeletedClassesValidator : AbstractValidator<GetDeletedClassesQuery>
{
    public GetDeletedClassesValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.Page <= 0 || x.PageSize <= 0 || x.Page <= int.MaxValue / x.PageSize)
            .WithMessage("Page and pageSize exceed the supported range.");
    }
}
