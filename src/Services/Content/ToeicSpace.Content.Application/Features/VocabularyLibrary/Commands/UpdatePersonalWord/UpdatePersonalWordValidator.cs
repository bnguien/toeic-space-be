using ToeicSpace.Content.Application.Common.Validation;
using ToeicSpace.Content.Domain.Entities;

namespace ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdatePersonalWord;

public sealed class UpdatePersonalWordValidator : AbstractValidator<UpdatePersonalWordCommand>
{
    public UpdatePersonalWordValidator()
    {
        RuleFor(x => x.LearnerId).NotEmpty();
        RuleFor(x => x.WordId).NotEmpty();
        RuleFor(x => x.Phonetic.Value).Must(x => Fits(x, SavedWord.MyPhoneticMaxLength)).WithName("phonetic")
            .WithMessage($"phonetic must be at most {SavedWord.MyPhoneticMaxLength} characters.").When(x => x.Phonetic.IsSet);
        RuleFor(x => x.PartOfSpeech.Value)
            .Must(x => string.IsNullOrWhiteSpace(x) || UpdatePersonalWordCommand.ParsePartOfSpeech(x) is not null)
            .WithName("partOfSpeech").WithMessage("partOfSpeech must be a known value.").When(x => x.PartOfSpeech.IsSet);
        RuleFor(x => x.MeaningVi.Value).Must(x => Fits(x, SavedWord.MyMeaningMaxLength)).WithName("meaningVi")
            .WithMessage($"meaningVi must be at most {SavedWord.MyMeaningMaxLength} characters.").When(x => x.MeaningVi.IsSet);
        RuleFor(x => x.Example.Value).Must(x => Fits(x, SavedWord.MyTextMaxLength)).WithName("example")
            .WithMessage($"example must be at most {SavedWord.MyTextMaxLength} characters.").When(x => x.Example.IsSet);
        RuleFor(x => x.ExampleVi.Value).Must(x => Fits(x, SavedWord.MyTextMaxLength)).WithName("exampleVi")
            .WithMessage($"exampleVi must be at most {SavedWord.MyTextMaxLength} characters.").When(x => x.ExampleVi.IsSet);
        RuleFor(x => x.Note.Value).Must(x => Fits(x, SavedWord.NoteMaxLength)).WithName("note")
            .WithMessage($"note must be at most {SavedWord.NoteMaxLength} characters.").When(x => x.Note.IsSet);
        RuleFor(x => x.TzOffset).ValidTzOffset();
    }

    private static bool Fits(string? value, int maxLength) => value is null || value.Trim().Length <= maxLength;
}
