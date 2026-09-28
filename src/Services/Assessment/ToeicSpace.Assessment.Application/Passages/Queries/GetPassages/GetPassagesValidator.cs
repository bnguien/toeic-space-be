namespace ToeicSpace.Assessment.Application.Passages.Queries.GetPassages;

public sealed class GetPassagesValidator : AbstractValidator<GetPassagesQuery>
{
    public GetPassagesValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Part)
            .IsInEnum();
    }
}
