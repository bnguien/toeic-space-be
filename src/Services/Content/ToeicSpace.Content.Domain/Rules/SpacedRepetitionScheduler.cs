using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Rules;

/// <summary>
/// SM-2 scheduler shared by Flashcard, Quiz and Reel. Pure: all dates are learner-local and passed in.
/// <list type="bullet">
/// <item>Quality: Easy 5, Good 4, Hard 2, Forget 1. q ≥ 3 lengthens (1, 6, then round(interval × EF'));
/// q &lt; 3 resets repetitions, sets interval 1 and counts a lapse.</item>
/// <item>EF' = EF + (0.1 − (5−q)(0.08 + (5−q)·0.02)), floor 1.3, start 2.5, applied before the interval is computed.</item>
/// <item>Good/Easy lengthen at most once per local day; Forget/Hard always apply. Interval cap 365 days.</item>
/// <item>Seen creates a new word due tomorrow and leaves a known word unchanged.</item>
/// </list>
/// </summary>
public static class SpacedRepetitionScheduler
{
    public const double DefaultEaseFactor = 2.5;
    public const double MinEaseFactor = 1.3;
    public const int MaxIntervalDays = 365;
    public const int MasteredIntervalDays = 21;

    public static int Quality(ReviewGrade grade) => grade switch
    {
        ReviewGrade.Easy => 5,
        ReviewGrade.Good => 4,
        ReviewGrade.Hard => 2,
        ReviewGrade.Forget => 1,
        _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "Seen has no SM-2 quality.")
    };

    /// <summary>State of a word the learner has never reviewed.</summary>
    public static ScheduleState Initial(DateOnly localToday) =>
        new(DefaultEaseFactor, 0, 0, 0, localToday, null, null);

    public static ScheduleResult Apply(ScheduleState? current, ReviewGrade grade, DateOnly localToday)
    {
        if (!Enum.IsDefined(grade)) throw new ArgumentOutOfRangeException(nameof(grade), grade, null);
        if (grade == ReviewGrade.Seen)
            return current is null
                ? new ScheduleResult(new ScheduleState(DefaultEaseFactor, 1, 0, 0, localToday.AddDays(1),
                    localToday, ReviewGrade.Seen), true)
                : new ScheduleResult(current, false);

        var state = current ?? Initial(localToday);
        if (grade is ReviewGrade.Good or ReviewGrade.Easy && LengthenedOn(state, localToday))
            return new ScheduleResult(state, false);

        var q = Quality(grade);
        var ease = NextEaseFactor(state.EaseFactor, q);
        int repetitions, interval, lapses = state.Lapses;
        if (q >= 3)
        {
            repetitions = state.Repetitions + 1;
            interval = repetitions switch
            {
                1 => 1,
                2 => 6,
                _ => (int)Math.Min(MaxIntervalDays, Math.Round(Math.Max(1, state.IntervalDays) * ease,
                    MidpointRounding.AwayFromZero))
            };
        }
        else
        {
            repetitions = 0;
            interval = 1;
            lapses++;
        }
        interval = Math.Clamp(interval, 1, MaxIntervalDays);
        return new ScheduleResult(new ScheduleState(ease, interval, repetitions, lapses,
            localToday.AddDays(interval), localToday, grade), true);
    }

    /// <summary>Days until the next review for each active grade if chosen now (0 = schedule kept).</summary>
    public static SchedulePreview Preview(ScheduleState? current, DateOnly localToday)
    {
        int Days(ReviewGrade grade)
        {
            var result = Apply(current, grade, localToday);
            return result.Changed ? result.State.IntervalDays : 0;
        }
        return new SchedulePreview(Days(ReviewGrade.Forget), Days(ReviewGrade.Hard),
            Days(ReviewGrade.Good), Days(ReviewGrade.Easy));
    }

    public static WordStatus StatusOf(int? intervalDays) => intervalDays switch
    {
        null => WordStatus.New,
        < MasteredIntervalDays => WordStatus.Learning,
        _ => WordStatus.Mastered
    };

    public static bool IsDue(DateOnly dueOn, DateOnly localToday) => dueOn <= localToday;

    /// <summary>Learner-local calendar day for a UTC instant and an offset in minutes east of UTC.</summary>
    public static DateOnly LocalDay(DateTime utcNow, int tzOffsetMinutes) =>
        DateOnly.FromDateTime(utcNow.AddMinutes(tzOffsetMinutes));

    private static bool LengthenedOn(ScheduleState state, DateOnly localToday) =>
        state.LastGradeDay == localToday && state.LastGrade is ReviewGrade.Good or ReviewGrade.Easy;

    private static double NextEaseFactor(double ease, int quality)
    {
        var miss = 5 - quality;
        // Every step is a multiple of 0.01, so rounding only removes binary floating-point noise.
        return Math.Max(MinEaseFactor, Math.Round(ease + (0.1 - miss * (0.08 + miss * 0.02)), 2));
    }
}
