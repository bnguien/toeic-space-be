namespace ToeicSpace.Content.Application.Features.VideoWords.Queries.GlossWords;

public sealed class GlossWordsValidator : AbstractValidator<GlossWordsQuery>
{
    public GlossWordsValidator()
    {
        RuleFor(x => x.Words).NotNull().WithMessage("words must be a list.");
        RuleForEach(x => x.Words).MaximumLength(100);
    }
}
