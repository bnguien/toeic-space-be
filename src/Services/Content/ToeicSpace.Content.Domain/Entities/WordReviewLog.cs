using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>Append-only record of one review; <see cref="ClientEventId"/> makes submissions idempotent.</summary>
public sealed class WordReviewLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public Guid WordId { get; set; }
    public StudyMode Mode { get; set; }
    public ReviewGrade Grade { get; set; }
    public Guid ClientEventId { get; set; }
    public DateTime ReviewedAt { get; set; }
    /// <summary>Learner-local date of the review, derived from the request's tzOffset.</summary>
    public DateOnly LocalDay { get; set; }
    public int IntervalBefore { get; set; }
    public int IntervalAfter { get; set; }
    public double EaseFactorBefore { get; set; }
    public double EaseFactorAfter { get; set; }
    /// <summary>False when the grade kept the schedule (Seen on a known word, second Good/Easy the same day).</summary>
    public bool ScheduleChanged { get; set; }
}
