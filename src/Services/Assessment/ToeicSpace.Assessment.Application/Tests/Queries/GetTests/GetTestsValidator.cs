namespace ToeicSpace.Assessment.Application.Tests.Queries.GetTests;

public sealed class GetTestsValidator : AbstractValidator<GetTestsQuery>
{
    public GetTestsValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Status)
            .IsInEnum();

        RuleFor(query => query.Category)
            .MaximumLength(100);

        RuleFor(query => query.Search)
            .MaximumLength(200);
    }
}
