using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Passages.Common;

public sealed class PassageContentValidator : AbstractValidator<IPassageContent>
{
    public PassageContentValidator()
    {
        RuleFor(passage => passage.Part)
            .IsInEnum()
            .Must(ToeicPartRules.AllowsPassage)
            .WithMessage("Passages are only used by Part 3, 4, 6 and 7.");

        RuleFor(passage => passage)
            .Must(passage => !string.IsNullOrWhiteSpace(passage.Content) || !string.IsNullOrWhiteSpace(passage.ImageUrl))
            .When(passage => ToeicPartRules.GetSection(passage.Part) == ToeicSection.Reading)
            .WithName("Content")
            .WithMessage("Part 6 and 7 passages need text content or an image.");

        RuleFor(passage => passage.PassageType)
            .MaximumLength(100);

        RuleFor(passage => passage.Title)
            .MaximumLength(500);

        RuleFor(passage => passage.AudioUrl)
            .MustBeOptionalHttpUrl();

        RuleFor(passage => passage.ImageUrl)
            .MustBeOptionalHttpUrls();

        RuleFor(passage => passage.OrderIndex)
            .GreaterThanOrEqualTo(0);

        RuleFor(passage => passage.Title).MustBeSafeRichText();
        RuleFor(passage => passage.Content).MustBeSafeRichText();
        RuleFor(passage => passage.Transcript).MustBeSafeRichText();
    }

    public static void ApplyContent(
        ToeicPassage passage,
        IPassageContent content)
    {
        passage.TestId = content.TestId;
        passage.Part = content.Part;
        passage.PassageType = TextNormalizer.NullIfEmpty(content.PassageType);
        passage.Title = TextNormalizer.NullIfEmpty(content.Title);
        passage.Content = TextNormalizer.NullIfEmpty(content.Content);
        passage.AudioUrl = TextNormalizer.NullIfEmpty(content.AudioUrl);
        passage.ImageUrl = TextNormalizer.NullIfEmpty(content.ImageUrl);
        passage.Transcript = TextNormalizer.NullIfEmpty(content.Transcript);
        passage.OrderIndex = content.OrderIndex;
    }
}
