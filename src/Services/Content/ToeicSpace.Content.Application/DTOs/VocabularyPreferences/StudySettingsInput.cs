using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Application.DTOs.VocabularyPreferences;

/// <summary>The <c>study</c> block of PUT me/preferences: only the fields sent (non-null) change.</summary>
public sealed record StudySettingsInput(int? ReelSeconds = null, int? QuizSeconds = null, bool? HintFirstLetter = null,
    bool? HintProgressive = null, bool? SpeakMeaning = null, string? FlashcardFront = null, bool? ShowExample = null,
    bool? Sfx = null, double? SfxVolume = null, string? TickStyle = null, string? ChimeStyle = null,
    string? Backdrop = null, string? CardStyle = null, string? Layout = null)
{
    /// <summary>Merges the sent fields into <paramref name="current"/> field by field.</summary>
    public StudySettings ApplyTo(StudySettings current) => current with
    {
        ReelSeconds = ReelSeconds ?? current.ReelSeconds,
        QuizSeconds = QuizSeconds ?? current.QuizSeconds,
        HintFirstLetter = HintFirstLetter ?? current.HintFirstLetter,
        HintProgressive = HintProgressive ?? current.HintProgressive,
        SpeakMeaning = SpeakMeaning ?? current.SpeakMeaning,
        FlashcardFront = FlashcardFront ?? current.FlashcardFront,
        ShowExample = ShowExample ?? current.ShowExample,
        Sfx = Sfx ?? current.Sfx,
        SfxVolume = SfxVolume is { } volume ? Math.Round(volume, 2) : current.SfxVolume,
        TickStyle = TickStyle ?? current.TickStyle,
        ChimeStyle = ChimeStyle ?? current.ChimeStyle,
        Backdrop = Backdrop ?? current.Backdrop,
        CardStyle = CardStyle ?? current.CardStyle,
        Layout = Layout ?? current.Layout
    };
}
