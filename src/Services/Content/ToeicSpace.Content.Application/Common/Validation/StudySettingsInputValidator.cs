using System.Linq.Expressions;
using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.Common.Validation;

/// <summary>Allowed values of the study settings (see <see cref="StudySettings"/>); absent fields are not checked.</summary>
public sealed class StudySettingsInputValidator : AbstractValidator<StudySettingsInput>
{
    public StudySettingsInputValidator()
    {
        RuleFor(x => x.ReelSeconds).Must(x => x is null || StudySettings.ReelSecondsValues.Contains(x.Value))
            .WithName("reelSeconds")
            .WithMessage($"reelSeconds must be one of {string.Join(", ", StudySettings.ReelSecondsValues)}.");
        RuleFor(x => x.QuizSeconds).Must(x => x is null || StudySettings.QuizSecondsValues.Contains(x.Value))
            .WithName("quizSeconds")
            .WithMessage($"quizSeconds must be one of {string.Join(", ", StudySettings.QuizSecondsValues)}.");
        RuleFor(x => x.SfxVolume).Must(x => x is null || StudySettings.IsValidSfxVolume(x.Value)).WithName("sfxVolume")
            .WithMessage("sfxVolume must be 0.1 to 1 in steps of 0.05.");
        OneOf(x => x.FlashcardFront, "flashcardFront", StudySettings.FlashcardFronts);
        OneOf(x => x.TickStyle, "tickStyle", StudySettings.TickStyles);
        OneOf(x => x.ChimeStyle, "chimeStyle", StudySettings.ChimeStyles);
        OneOf(x => x.Backdrop, "backdrop", StudySettings.Backdrops);
        OneOf(x => x.CardStyle, "cardStyle", StudySettings.CardStyles);
        OneOf(x => x.Layout, "layout", StudySettings.Layouts);
    }

    private void OneOf(Expression<Func<StudySettingsInput, string?>> field, string name, string[] allowed) =>
        RuleFor(field).Must(x => x is null || allowed.Contains(x)).WithName(name)
            .WithMessage($"{name} must be one of {string.Join(", ", allowed)}.");
}
