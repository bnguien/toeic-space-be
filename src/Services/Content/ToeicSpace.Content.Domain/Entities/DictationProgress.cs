namespace ToeicSpace.Content.Domain.Entities;

/// <summary>Best result of one learner (JWT <c>sub</c>) on one sentence; drives unlocks, stars and map counts.</summary>
public sealed class DictationProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public int ItemId { get; set; }
    public DictationItem? Item { get; set; }
    public double BestAccuracy { get; set; }
    public int BestScore { get; set; }
    public bool Passed { get; set; }
    /// <summary>At least one pass without hints, keywords or meaning opened before submitting.</summary>
    public bool PassedClean { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? LastAttemptAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }

    /// <summary>Records one graded submission (Studychill <c>check</c> rules; the best values never go down).</summary>
    public void Record(double accuracy, int score, bool passed, bool assisted, DateTime now)
    {
        AttemptCount++;
        LastAttemptAt = now;
        if (accuracy >= BestAccuracy) BestAccuracy = accuracy;
        if (score > BestScore) BestScore = score;
        if (passed) Passed = true;
        if (passed && !assisted) PassedClean = true;
        UpdatedAt = now;
        Version++;
    }
}
