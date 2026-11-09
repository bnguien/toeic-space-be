using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetMistakes;

public sealed class GetMistakesValidator : AbstractValidator<GetMistakesQuery>
{
    public GetMistakesValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Page).InclusiveBetween(1, 100_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, GetMistakesQuery.MaxPageSize);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
