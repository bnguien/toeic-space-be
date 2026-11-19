using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// A server-side study session (Flashcard, Quiz or Reel) so a learner can resume on any device. At most one session
/// per (learner, mode, source) is active: <see cref="ActiveSlot"/> is set only while active and is unique per learner.
/// </summary>
public sealed class StudySession
{
    /// <summary>A Forget/Hard card comes back after this many other cards (once per word).</summary>
    public const int RequeueGap = 4;
    /// <summary>An active session untouched for longer than this is treated as abandoned.</summary>
    public static readonly TimeSpan StaleAfter = TimeSpan.FromDays(7);

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public StudyMode Mode { get; set; }
    public StudySourceKind SourceKind { get; set; }
    public Guid? SourceId { get; set; }
    public string? SourceTitle { get; set; }
    /// <summary>Word ids in study order, re-queued cards included.</summary>
    public List<Guid> Queue { get; set; } = [];
    public int Position { get; set; }
    public List<Guid> Requeued { get; set; } = [];
    /// <summary>Last grade of each word in this session.</summary>
    public Dictionary<Guid, ReviewGrade> Results { get; set; } = [];
    public int CorrectCount { get; set; }
    public int ReviewCount { get; set; }
    public StudySessionStatus Status { get; set; } = StudySessionStatus.Active;
    public string? ActiveSlot { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int Version { get; set; }

    public static string SlotOf(StudyMode mode, StudySourceKind kind, Guid? sourceId) =>
        $"{mode}:{kind}:{sourceId?.ToString("N") ?? "-"}";

    public bool IsStale(DateTime now) => Status == StudySessionStatus.Active && now - UpdatedAt > StaleAfter;

    /// <summary>Status as seen by the learner: a stale active session reads as abandoned.</summary>
    public StudySessionStatus EffectiveStatus(DateTime now) => IsStale(now) ? StudySessionStatus.Abandoned : Status;

    public bool IsOpen(DateTime now) => EffectiveStatus(now) == StudySessionStatus.Active;

    public Guid? CurrentWordId => Position < Queue.Count ? Queue[Position] : null;

    public void Activate(DateTime now)
    {
        Status = StudySessionStatus.Active;
        ActiveSlot = SlotOf(Mode, SourceKind, SourceId);
        StartedAt = now;
        UpdatedAt = now;
    }

    /// <summary>Records the grade of the current card, re-queues a first Forget/Hard and moves to the next card.</summary>
    public void Answer(Guid wordId, ReviewGrade grade, DateTime now)
    {
        Results[wordId] = grade;
        ReviewCount++;
        if (grade is ReviewGrade.Good or ReviewGrade.Easy) CorrectCount++;
        if (grade is ReviewGrade.Forget or ReviewGrade.Hard && !Requeued.Contains(wordId))
        {
            Queue.Insert(Math.Min(Position + 1 + RequeueGap, Queue.Count), wordId);
            Requeued.Add(wordId);
        }
        Position++;
        Touch(now);
    }

    public void Complete(DateTime now)
    {
        Status = StudySessionStatus.Completed;
        CompletedAt = now;
        ActiveSlot = null;
        Touch(now);
    }

    public void Abandon(DateTime now)
    {
        Status = StudySessionStatus.Abandoned;
        ActiveSlot = null;
        Touch(now);
    }

    private void Touch(DateTime now)
    {
        UpdatedAt = now;
        Version++;
    }
}
