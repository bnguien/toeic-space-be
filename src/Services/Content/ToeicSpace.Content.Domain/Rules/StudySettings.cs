namespace ToeicSpace.Content.Domain.Rules;

/// <summary>
/// Study-mode settings of a learner (timers, hints, voice, sound effects, card colours, backdrop, frame orientation), stored as one JSON
/// value on <see cref="Entities.LearnerVocabularyPreferences"/>. The allowed values below are the only ones accepted.
/// </summary>
public sealed record StudySettings
{
    public static readonly int[] ReelSecondsValues = [3, 5, 8, 12];
    /// <summary>0 = no timer.</summary>
    public static readonly int[] QuizSecondsValues = [0, 10, 15, 20, 30];
    public const double MinSfxVolume = 0.1;
    public const double MaxSfxVolume = 1;
    public const double SfxVolumeStep = 0.05;
    public static readonly string[] FlashcardFronts = ["word", "meaning"];
    public static readonly string[] TickStyles = ["wood", "mechanical", "wall", "digital", "drop", "drum", "heart", "off"];
    public static readonly string[] ChimeStyles = ["bell", "game", "soft"];
    public static readonly string[] Backdrops = ["video", "ambient"];
    public static readonly string[] CardStyles = ["stage", "pastel", "night"];
    public static readonly string[] Layouts = ["portrait", "landscape"];

    public static StudySettings Default { get; } = new();

    public int ReelSeconds { get; init; } = 5;
    public int QuizSeconds { get; init; } = 15;
    public bool HintFirstLetter { get; init; } = true;
    /// <summary>Reveal more letters as the Reel countdown runs out.</summary>
    public bool HintProgressive { get; init; } = true;
    /// <summary>Also read the Vietnamese meaning aloud.</summary>
    public bool SpeakMeaning { get; init; }
    public string FlashcardFront { get; init; } = "word";
    public bool ShowExample { get; init; } = true;
    public bool Sfx { get; init; } = true;
    public double SfxVolume { get; init; } = 0.8;
    public string TickStyle { get; init; } = "wood";
    public string ChimeStyle { get; init; } = "bell";
    public string Backdrop { get; init; } = "video";
    public string CardStyle { get; init; } = "stage";
    /// <summary>Study frame orientation (phones always render portrait).</summary>
    public string Layout { get; init; } = "portrait";

    /// <summary>True for 0.1 … 1 in steps of 0.05.</summary>
    public static bool IsValidSfxVolume(double value)
    {
        const double tolerance = 1e-6;
        var steps = value / SfxVolumeStep;
        return value >= MinSfxVolume - tolerance && value <= MaxSfxVolume + tolerance
            && Math.Abs(steps - Math.Round(steps)) < tolerance;
    }
}
