using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Application.Services.Enrichment;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.LibraryImport.Commands.ImportWords;

public sealed class ImportWordsValidator : AbstractValidator<ImportWordsCommand>
{
    public ImportWordsValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.Items).NotNull().WithMessage("items is required.");
        RuleFor(x => x.Items.Count).InclusiveBetween(1, ImportWordsCommand.MaxItems)
            .WithMessage($"items must contain 1 to {ImportWordsCommand.MaxItems} entries.").When(x => x.Items is not null);
        RuleForEach(x => x.Items).ChildRules(item =>
        {
            item.RuleFor(x => x.Headword).NotEmpty().MaximumLength(InputNormalizer.MaxLength);
            item.RuleFor(x => x.MeaningVi).MaximumLength(VocabularyWord.CustomMeaningMaxLength);
            item.RuleFor(x => x.Phonetic).MaximumLength(CustomWordInputValidator.PhoneticMaxLength);
            item.RuleFor(x => x.PartOfSpeech).IsInEnum().When(x => x.PartOfSpeech.HasValue);
            item.RuleFor(x => x.Example).MaximumLength(VocabularyWord.CustomTextMaxLength);
            item.RuleFor(x => x.ExampleVi).MaximumLength(VocabularyWord.CustomTextMaxLength);
            item.RuleFor(x => x.Category).MaximumLength(VocabularyFolder.NameMaxLength);
        }).When(x => x.Items is not null);
        RuleFor(x => x.FolderId).NotEqual(Guid.Empty).When(x => x.FolderId.HasValue);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }
}
