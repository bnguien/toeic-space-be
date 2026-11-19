using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticle;

public sealed class GetNewsArticleValidator : AbstractValidator<GetNewsArticleQuery>
{
    public GetNewsArticleValidator() => RuleFor(x => x.Slug).NewsSlug();
}
