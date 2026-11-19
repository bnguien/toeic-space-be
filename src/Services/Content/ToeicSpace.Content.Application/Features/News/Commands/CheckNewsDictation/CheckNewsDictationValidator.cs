using ToeicSpace.Content.Application.Common.Validation;

namespace ToeicSpace.Content.Application.Features.News.Commands.CheckNewsDictation;

public sealed class CheckNewsDictationValidator : AbstractValidator<CheckNewsDictationCommand>
{
    public CheckNewsDictationValidator()
    {
        RuleFor(x => x.Slug).NewsSlug();
        RuleFor(x => x.SentenceId).NotEmpty().Matches(NewsRules.SentenceIdPattern)
            .WithMessage("sentenceId must look like p1s1.");
        RuleFor(x => x.Text).NotEmpty().MaximumLength(NewsRules.DictationInputMaxLength);
    }
}
