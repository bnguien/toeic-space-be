using ToeicSpace.Assessment.Domain.Common;
using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Domain.Entities;

public class ToeicAttempt : BaseAuditableEntity
{
    public Guid UserId { get; set; }

    public Guid? TestId { get; set; }

    public ToeicTest? Test { get; set; }

    public Guid? PracticeSetId { get; set; }
    public DateTime? LastActivityAt { get; set; }
    public ToeicAttemptSnapshot? Snapshot { get; set; }

    public DateTime StartTime { get; set; }

    public DateTime? EndTime { get; set; }

    public int DurationSeconds { get; set; }

    public int TotalScore { get; set; }

    public int ListeningScore { get; set; }

    public int ReadingScore { get; set; }

    public int TotalCorrect { get; set; }

    public int TotalQuestions { get; set; }

    public AttemptStatus Status { get; set; }

    public AttemptMode Mode { get; set; }

    /// <summary>
    /// Display name captured at start (e.g. "Part 5 · 20 câu ngẫu nhiên · 18/11/2026").
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Comma-separated TOEIC parts covered by the attempt, e.g. "1,2,3,4,5,6,7".
    /// </summary>
    public string? SelectedParts { get; set; }

    /// <summary>
    /// Server deadline of a timed (FullTest) attempt. Null for untimed practice.
    /// </summary>
    public DateTime? Deadline { get; set; }

    public int? TimeLimitSeconds { get; set; }

    public int? ListeningCorrect { get; set; }

    public int? ReadingCorrect { get; set; }

    public ICollection<ToeicAttemptAnswer> Answers { get; set; } = new List<ToeicAttemptAnswer>();
}
