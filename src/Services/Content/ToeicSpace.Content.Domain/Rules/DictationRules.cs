namespace ToeicSpace.Content.Domain.Rules;

/// <summary>
/// Layered help of the dictation roadmap (Studychill <c>dictation_coach</c>): help comes after the learner's own
/// attempt, fades with the level, and a pass is never a guess.
/// <list type="bullet">
/// <item>From <see cref="CleanPassLevel"/> a pass helped by hints / keywords / meaning opened before submitting
/// does not unlock the next sentence; levels 1–2 unlock on any pass.</item>
/// <item>From <see cref="SupportGatedLevel"/> keywords and meaning open only after the first submission.</item>
/// <item>A sentence passes only when EVERY word is correct (client and server share this rule).</item>
/// </list>
/// </summary>
public static class DictationRules
{
    public const int CleanPassLevel = 3;
    public const int SupportGatedLevel = 3;
    public const int LevelOneListHideStreak = 5;
    public const double AssistedRateWarning = 0.6;
    public const int SuggestLevelUpStreak = 8;
    public const int PassScore = 10;
    /// <summary>% of correct words needed to pass: all of them.</summary>
    public const double PassThreshold = 100;
    public const int MaxHintCount = 99;

    /// <summary>Whether this sentence's progress opens the next sentence.</summary>
    public static bool PassesUnlock(int level, bool passed, bool passedClean) =>
        level < CleanPassLevel ? passed : passedClean;

    /// <summary>Score of a submission: <see cref="PassScore"/> on a pass, else accuracy / 10 rounded (half to even).</summary>
    public static int Score(bool passed, double accuracy) =>
        passed ? PassScore : (int)Math.Round(accuracy / 10, MidpointRounding.ToEven);

    /// <summary>Stars of a finished stage from the average best accuracy of its sentences.</summary>
    public static int StageStars(IReadOnlyCollection<double> accuracies)
    {
        var average = accuracies.Count == 0 ? 0 : accuracies.Average();
        return average >= 95 ? 3 : average >= 85 ? 2 : 1;
    }
}
