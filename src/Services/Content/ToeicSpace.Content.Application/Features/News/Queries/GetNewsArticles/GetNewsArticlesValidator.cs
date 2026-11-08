using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticles;

public sealed class GetNewsArticlesValidator : AbstractValidator<GetNewsArticlesQuery>
{
    public GetNewsArticlesValidator()
    {
        RuleFor(x => x.Stage).MaximumLength(8);
        RuleFor(x => x.Topic).MaximumLength(20);
        RuleFor(x => x.Q).MaximumLength(GetNewsArticlesQuery.MaxQueryLength);
        RuleFor(x => x.Length).Must(x => string.IsNullOrEmpty(x) || NewsCatalog.Lengths.Contains(x))
            .WithMessage("length must be short, medium or long.");
        RuleFor(x => x.State).Must(x => string.IsNullOrEmpty(x) || NewsCatalog.States.Contains(x))
            .WithMessage("state must be reading or done.");
        RuleFor(x => x.Page).InclusiveBetween(1, GetNewsArticlesQuery.MaxPage);
        RuleFor(x => x.PageSize).InclusiveBetween(1, NewsCatalog.MaxPageSize);
    }
}
