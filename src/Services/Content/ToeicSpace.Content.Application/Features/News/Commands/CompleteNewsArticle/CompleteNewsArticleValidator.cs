using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.News.Commands.CompleteNewsArticle;

public sealed class CompleteNewsArticleValidator : AbstractValidator<CompleteNewsArticleCommand>
{
    public CompleteNewsArticleValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Slug).NewsSlug();
    }
}
