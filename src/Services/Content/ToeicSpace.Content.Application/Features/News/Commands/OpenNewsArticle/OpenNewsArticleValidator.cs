using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.News.Commands.OpenNewsArticle;

public sealed class OpenNewsArticleValidator : AbstractValidator<OpenNewsArticleCommand>
{
    public OpenNewsArticleValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Slug).NewsSlug();
    }
}
