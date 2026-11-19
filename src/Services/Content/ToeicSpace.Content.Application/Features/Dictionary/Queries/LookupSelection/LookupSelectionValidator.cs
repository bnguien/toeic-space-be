using System.Text.RegularExpressions;

namespace ToeicSpace.Content.Application.Features.Dictionary.Queries.LookupSelection;

public sealed partial class LookupSelectionValidator : AbstractValidator<LookupSelectionQuery>
{
    public LookupSelectionValidator()
    {
        RuleFor(x => x.Text).NotEmpty().MaximumLength(LookupSelectionQuery.MaxTextLength)
            .Must(text => LatinLetter().IsMatch(text ?? string.Empty))
            .WithMessage("text must contain at least one latin letter.");
        RuleFor(x => x.Context).MaximumLength(LookupSelectionQuery.MaxContextLength);
    }

    [GeneratedRegex("[A-Za-z]")]
    private static partial Regex LatinLetter();
}
