namespace ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;

public sealed class GetPracticeSetsValidator : AbstractValidator<GetPracticeSetsQuery>
{
    public GetPracticeSetsValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Part)
            .IsInEnum();

        RuleFor(query => query.Kind)
            .IsInEnum();

        RuleFor(query => query.Level)
            .InclusiveBetween(1, 5);

        RuleFor(query => query.Status)
            .IsInEnum();

        RuleFor(query => query.Search)
            .MaximumLength(200);
    }
}
