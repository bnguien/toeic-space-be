using ToeicSpace.Assessment.Domain.Common;
using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Domain.Entities;

public class ToeicAttempt : BaseAuditableEntity
{
    public Guid UserId { get; set; }

    public Guid TestId { get; set; }

    public ToeicTest Test { get; set; } = null!;

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

    public ICollection<ToeicAttemptAnswer> Answers { get; set; } = new List<ToeicAttemptAnswer>();
}
