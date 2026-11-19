using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class SpacedRepetitionSchedulerTests
{
    private static readonly DateOnly Day0 = new(2026, 11, 1);

    private static ScheduleState Known(int interval, int repetitions, double ease = 2.5, int lapses = 0) =>
        new(ease, interval, repetitions, lapses, Day0, Day0.AddDays(-interval), ReviewGrade.Good);

    [Fact]
    public void Good_and_easy_on_a_new_word_start_at_one_day()
    {
        var good = SpacedRepetitionScheduler.Apply(null, ReviewGrade.Good, Day0);
        Assert.True(good.Changed);
        Assert.Equal(new ScheduleState(2.5, 1, 1, 0, Day0.AddDays(1), Day0, ReviewGrade.Good), good.State);

        var easy = SpacedRepetitionScheduler.Apply(null, ReviewGrade.Easy, Day0).State;
        Assert.Equal((2.6, 1, 1), (easy.EaseFactor, easy.IntervalDays, easy.Repetitions));
    }

    [Fact]
    public void Good_streak_follows_sm2_intervals_1_6_then_interval_times_ease()
    {
        var day = Day0;
        ScheduleState? state = null;
        var intervals = new List<int>();
        for (var i = 0; i < 4; i++)
        {
            state = SpacedRepetitionScheduler.Apply(state, ReviewGrade.Good, day).State;
            intervals.Add(state.IntervalDays);
            Assert.Equal(day.AddDays(state.IntervalDays), state.DueOn);
            day = state.DueOn;
        }
        // 6 × 2.5 = 15; 15 × 2.5 = 37.5 → 38 (away from zero). Good leaves EF at 2.5.
        Assert.Equal([1, 6, 15, 38], intervals);
        Assert.Equal(2.5, state!.EaseFactor);
        Assert.Equal(4, state.Repetitions);
    }

    [Fact]
    public void Easy_raises_ease_before_computing_the_interval()
    {
        var day = Day0;
        ScheduleState? state = null;
        var seen = new List<(int, double)>();
        for (var i = 0; i < 3; i++)
        {
            state = SpacedRepetitionScheduler.Apply(state, ReviewGrade.Easy, day).State;
            seen.Add((state.IntervalDays, state.EaseFactor));
            day = state.DueOn;
        }
        Assert.Equal([(1, 2.6), (6, 2.7), (17, 2.8)], seen); // 6 × 2.8 = 16.8 → 17
    }

    [Theory]
    [InlineData(ReviewGrade.Hard, 2.18)]
    [InlineData(ReviewGrade.Forget, 1.96)]
    public void Hard_and_forget_reset_to_one_day_and_count_a_lapse(ReviewGrade grade, double ease)
    {
        var result = SpacedRepetitionScheduler.Apply(Known(15, 3, lapses: 1), grade, Day0);
        Assert.True(result.Changed);
        Assert.Equal(new ScheduleState(ease, 1, 0, 2, Day0.AddDays(1), Day0, grade), result.State);
    }

    [Fact]
    public void Ease_factor_never_drops_below_1_3()
    {
        ScheduleState? state = null;
        for (var i = 0; i < 6; i++)
            state = SpacedRepetitionScheduler.Apply(state, ReviewGrade.Forget, Day0.AddDays(i)).State;
        Assert.Equal(SpacedRepetitionScheduler.MinEaseFactor, state!.EaseFactor);
        Assert.Equal(6, state.Lapses);
    }

    [Fact]
    public void Interval_is_capped_at_365_days()
    {
        var result = SpacedRepetitionScheduler.Apply(Known(200, 5), ReviewGrade.Easy, Day0).State;
        Assert.Equal(365, result.IntervalDays);
        Assert.Equal(Day0.AddDays(365), result.DueOn);
    }

    [Fact]
    public void Good_or_easy_lengthen_at_most_once_per_local_day_but_forget_and_hard_always_apply()
    {
        var first = SpacedRepetitionScheduler.Apply(Known(6, 2), ReviewGrade.Good, Day0);
        Assert.Equal(15, first.State.IntervalDays);

        var again = SpacedRepetitionScheduler.Apply(first.State, ReviewGrade.Good, Day0);
        Assert.False(again.Changed);
        Assert.Same(first.State, again.State);
        Assert.False(SpacedRepetitionScheduler.Apply(first.State, ReviewGrade.Easy, Day0).Changed);

        var hard = SpacedRepetitionScheduler.Apply(first.State, ReviewGrade.Hard, Day0);
        Assert.True(hard.Changed);
        Assert.Equal(1, hard.State.IntervalDays);
        // After a lapse the word is relearned: Good applies again from repetition 1.
        var relearn = SpacedRepetitionScheduler.Apply(hard.State, ReviewGrade.Good, Day0);
        Assert.True(relearn.Changed);
        Assert.Equal((1, 1), (relearn.State.IntervalDays, relearn.State.Repetitions));

        // The next local day lengthens again.
        Assert.True(SpacedRepetitionScheduler.Apply(first.State, ReviewGrade.Good, Day0.AddDays(1)).Changed);
    }

    [Fact]
    public void Seen_creates_a_new_word_due_tomorrow_and_leaves_known_words_unchanged()
    {
        var created = SpacedRepetitionScheduler.Apply(null, ReviewGrade.Seen, Day0);
        Assert.True(created.Changed);
        Assert.Equal((1, 0, 0, 2.5), (created.State.IntervalDays, created.State.Repetitions, created.State.Lapses,
            created.State.EaseFactor));
        Assert.Equal(Day0.AddDays(1), created.State.DueOn);

        var known = Known(15, 3);
        var kept = SpacedRepetitionScheduler.Apply(known, ReviewGrade.Seen, Day0);
        Assert.False(kept.Changed);
        Assert.Same(known, kept.State);

        // Seen does not block a real Good later that day.
        Assert.True(SpacedRepetitionScheduler.Apply(created.State, ReviewGrade.Good, Day0).Changed);
    }

    [Fact]
    public void Preview_returns_days_per_grade_and_zero_when_the_schedule_is_kept()
    {
        Assert.Equal(new SchedulePreview(1, 1, 1, 1), SpacedRepetitionScheduler.Preview(null, Day0));
        // 6 × 2.5 = 15; 6 × 2.6 = 15.6 → 16.
        Assert.Equal(new SchedulePreview(1, 1, 15, 16), SpacedRepetitionScheduler.Preview(Known(6, 2), Day0));

        var reviewedToday = SpacedRepetitionScheduler.Apply(Known(6, 2), ReviewGrade.Good, Day0).State;
        Assert.Equal(new SchedulePreview(1, 1, 0, 0), SpacedRepetitionScheduler.Preview(reviewedToday, Day0));
    }

    [Fact]
    public void Status_due_and_local_day_follow_the_contract()
    {
        Assert.Equal(WordStatus.New, SpacedRepetitionScheduler.StatusOf(null));
        Assert.Equal(WordStatus.Learning, SpacedRepetitionScheduler.StatusOf(1));
        Assert.Equal(WordStatus.Learning, SpacedRepetitionScheduler.StatusOf(20));
        Assert.Equal(WordStatus.Mastered, SpacedRepetitionScheduler.StatusOf(21));
        Assert.True(SpacedRepetitionScheduler.IsDue(Day0, Day0));
        Assert.True(SpacedRepetitionScheduler.IsDue(Day0.AddDays(-3), Day0));
        Assert.False(SpacedRepetitionScheduler.IsDue(Day0.AddDays(1), Day0));

        var utc = new DateTime(2026, 11, 1, 20, 0, 0, DateTimeKind.Utc);
        Assert.Equal(new DateOnly(2026, 11, 2), SpacedRepetitionScheduler.LocalDay(utc, 420));
        Assert.Equal(new DateOnly(2026, 11, 1), SpacedRepetitionScheduler.LocalDay(utc, 0));
        Assert.Equal(new DateOnly(2026, 11, 1), SpacedRepetitionScheduler.LocalDay(utc, -720));
        Assert.Equal(new DateOnly(2026, 11, 2), SpacedRepetitionScheduler.LocalDay(utc, 840));
    }

    [Fact]
    public void Quality_maps_grades_and_rejects_seen()
    {
        Assert.Equal([5, 4, 2, 1], new[] { ReviewGrade.Easy, ReviewGrade.Good, ReviewGrade.Hard, ReviewGrade.Forget }
            .Select(SpacedRepetitionScheduler.Quality));
        Assert.Throws<ArgumentOutOfRangeException>(() => SpacedRepetitionScheduler.Quality(ReviewGrade.Seen));
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SpacedRepetitionScheduler.Apply(null, (ReviewGrade)99, Day0));
    }
}
