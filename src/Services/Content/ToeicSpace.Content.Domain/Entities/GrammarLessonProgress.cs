namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// A learner's progress in one grammar topic: confirmed parts, best quick-check score per unit, best mini-test score
/// and the completion time (theory + vocabulary confirmed + mini test passed; kept once reached).
/// </summary>
public sealed class GrammarLessonProgress
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime? TheoryConfirmedAt { get; set; }
    public DateTime? VocabConfirmedAt { get; set; }
    /// <summary>Best quick-check score per unit number ("1" → 3).</summary>
    public Dictionary<string, int> QuickCheckBest { get; set; } = [];
    public int? MiniTestBest { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public int Version { get; set; }

    public void Touch(DateTime now)
    {
        UpdatedAt = now;
        Version++;
    }
}
