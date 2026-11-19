using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Common.Validation;

/// <summary>Field rules of a learner's custom word (create and edit).</summary>
public sealed class CustomWordInputValidator : AbstractValidator<CustomWordInput>
{
    public const int PhoneticMaxLength = 100;

    public CustomWordInputValidator()
    {
        RuleFor(x => x.Headword).Must(x => !string.IsNullOrWhiteSpace(x)
                && x.Trim().Length <= VocabularyWord.CustomHeadwordMaxLength)
            .WithMessage($"headword must be 1 to {VocabularyWord.CustomHeadwordMaxLength} characters.");
        RuleFor(x => x.MeaningVi).Must(x => !string.IsNullOrWhiteSpace(x)
                && x.Trim().Length <= VocabularyWord.CustomMeaningMaxLength)
            .WithMessage($"meaningVi must be 1 to {VocabularyWord.CustomMeaningMaxLength} characters.");
        RuleFor(x => x.PartOfSpeech).IsInEnum().WithMessage("partOfSpeech must be a known value.");
        RuleFor(x => x.Phonetic).MaximumLength(PhoneticMaxLength);
        RuleFor(x => x.DefinitionEn).MaximumLength(VocabularyWord.CustomTextMaxLength);
        RuleFor(x => x.Example).MaximumLength(VocabularyWord.CustomTextMaxLength);
        RuleFor(x => x.ExampleVi).MaximumLength(VocabularyWord.CustomTextMaxLength);
    }
}
