using ToeicSpace.Content.Application.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;

namespace ToeicSpace.Content.Application.Features.VocabularyLookup.Queries.LookupWord;

public sealed class LookupWordValidator : AbstractValidator<LookupWordQuery>
{
    public LookupWordValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Text).NotEmpty().MaximumLength(LookupWordQuery.MaxTextLength)
            .Must(x => InputNormalizer.Normalize(x).Kind != WordKind.Reject)
            .WithMessage("text must be a word or a phrase of at most 5 words with latin letters.");
        RuleFor(x => x.Context).MaximumLength(LookupWordQuery.MaxContextLength);
    }
}
