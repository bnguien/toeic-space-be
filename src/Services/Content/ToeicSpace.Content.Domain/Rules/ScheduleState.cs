using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Rules;

/// <summary>Spaced-repetition state of one (learner, word) pair.</summary>
public sealed record ScheduleState(double EaseFactor, int IntervalDays, int Repetitions, int Lapses,
    DateOnly DueOn, DateOnly? LastGradeDay, ReviewGrade? LastGrade);

/// <summary>Outcome of one grade. <see cref="Changed"/> is false when the schedule was kept as it was.</summary>
public sealed record ScheduleResult(ScheduleState State, bool Changed);

/// <summary>Days until the next review for each grade; 0 means the current schedule is kept.</summary>
public sealed record SchedulePreview(int Forget, int Hard, int Good, int Easy);
