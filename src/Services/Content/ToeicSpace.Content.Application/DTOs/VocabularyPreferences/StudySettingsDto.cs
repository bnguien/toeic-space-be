namespace ToeicSpace.Content.Application.DTOs.VocabularyPreferences;

/// <summary>Study-mode settings; defaults when the learner never saved any.</summary>
public sealed record StudySettingsDto(int ReelSeconds, int QuizSeconds, bool HintFirstLetter, bool HintProgressive,
    bool SpeakMeaning, string FlashcardFront, bool ShowExample, bool Sfx, double SfxVolume, string TickStyle,
    string ChimeStyle, string Backdrop, string CardStyle, string Layout);
