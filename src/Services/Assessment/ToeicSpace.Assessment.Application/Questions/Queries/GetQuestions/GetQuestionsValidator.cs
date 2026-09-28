namespace ToeicSpace.Assessment.Application.Questions.Queries.GetQuestions;

public sealed class GetQuestionsValidator : AbstractValidator<GetQuestionsQuery>
{
    public GetQuestionsValidator()
    {
        RuleFor(query => query.Page)
            .GreaterThanOrEqualTo(1);

        RuleFor(query => query.PageSize)
            .InclusiveBetween(1, 100);

        RuleFor(query => query.Part)
            .IsInEnum();

        RuleFor(query => query.Section)
            .IsInEnum();

        RuleFor(query => query.DifficultyLevel)
            .IsInEnum();

        RuleFor(query => query.Status)
            .IsInEnum();

        RuleFor(query => query.Search)
            .MaximumLength(200);
    }
}
