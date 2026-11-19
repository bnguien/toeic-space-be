using ToeicSpace.Content.Application.Features.VocabularyLookup.Queries.LookupWord;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Application.Features.News.Queries.LookupNewsWord;

public sealed class LookupNewsWordValidator : AbstractValidator<LookupNewsWordQuery>
{
    public LookupNewsWordValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(LookupWordQuery.MaxTextLength)
            .Must(x => InputNormalizer.Normalize(x).Kind != WordKind.Reject)
            .WithMessage("text must be a word or a phrase of at most 5 words with latin letters.");
        RuleFor(x => x.Context).MaximumLength(LookupWordQuery.MaxContextLength);
    }
}
