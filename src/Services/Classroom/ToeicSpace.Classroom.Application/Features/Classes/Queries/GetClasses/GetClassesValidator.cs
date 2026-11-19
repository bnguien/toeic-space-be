namespace ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;

public sealed class GetClassesValidator : AbstractValidator<GetClassesQuery>
{
    public GetClassesValidator()
    {
        RuleFor(x => x.Page).GreaterThan(0);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x).Must(x => x.Page <= 0 || x.PageSize <= 0 || x.Page <= int.MaxValue / x.PageSize)
            .WithMessage("Page and pageSize exceed the supported range.");
        RuleFor(x => x).Must(x => x.StartDateFrom is null || x.StartDateTo is null || x.StartDateFrom <= x.StartDateTo)
            .WithMessage("StartDateFrom cannot exceed StartDateTo.");
    }
}
