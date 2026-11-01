using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>Spaced-repetition state of one word for one learner (JWT <c>sub</c>; no foreign key to Identity).</summary>
public sealed class LearnerWordProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public Guid WordId { get; set; }
    public VocabularyWord? Word { get; set; }
    public double EaseFactor { get; set; } = SpacedRepetitionScheduler.DefaultEaseFactor;
    public int IntervalDays { get; set; }
    public int Repetitions { get; set; }
    public int Lapses { get; set; }
    /// <summary>Learner-local date on which the word is due again.</summary>
    public DateOnly DueOn { get; set; }
    public DateTime? LastReviewedAt { get; set; }
    public DateOnly? LastGradeDay { get; set; }
    public ReviewGrade? LastGrade { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }

    public ScheduleState ToState() =>
        new(EaseFactor, IntervalDays, Repetitions, Lapses, DueOn, LastGradeDay, LastGrade);

    /// <summary>Stores a new schedule produced by <see cref="SpacedRepetitionScheduler"/>.</summary>
    public void Apply(ScheduleState state, DateTime now)
    {
        EaseFactor = state.EaseFactor;
        IntervalDays = state.IntervalDays;
        Repetitions = state.Repetitions;
        Lapses = state.Lapses;
        DueOn = state.DueOn;
        LastGradeDay = state.LastGradeDay;
        LastGrade = state.LastGrade;
        LastReviewedAt = now;
        UpdatedAt = now;
        Version++;
    }
}
