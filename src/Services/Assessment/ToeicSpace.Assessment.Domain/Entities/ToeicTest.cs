using ToeicSpace.Assessment.Domain.Common;
using ToeicSpace.Assessment.Domain.Enums;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Domain.Entities;

/// <summary>
/// A full TOEIC test (standard 200 questions, 120 minutes). Practice collections are
/// modelled separately as <see cref="ToeicPracticeSet"/>.
/// </summary>
public class ToeicTest : BaseAuditableEntity
{
    public string Title { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string? Description { get; set; }

    /// <summary>
    /// Test series used for grouping in the UI (e.g. "ETS 2026", "Crack TOEIC Vol 1").
    /// </summary>
    public string? Category { get; set; }

    public int? Year { get; set; }

    public int TotalQuestions { get; set; } = ToeicPartRules.StandardTestQuestionCount;

    public int DurationMinutes { get; set; } = ToeicPartRules.StandardTestDurationMinutes;

    public int TotalListeningQuestions { get; set; } = ToeicPartRules.StandardSectionQuestionCount;

    public int TotalReadingQuestions { get; set; } = ToeicPartRules.StandardSectionQuestionCount;

    public string? AudioUrl { get; set; }

    public bool IsActive { get; set; } = true;

    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// External ID mapped from source databases (e.g. Studychill test UUID).
    /// </summary>
    public Guid? ExternalId { get; set; }

    public string? Source { get; set; }

    /// <summary>
    /// Extra metadata stored in JSON format.
    /// </summary>
    public string? Metadata { get; set; }

    public DateTime? DeletedAt { get; set; }

    public ICollection<ToeicPassage> Passages { get; set; } = new List<ToeicPassage>();

    public ICollection<ToeicQuestion> Questions { get; set; } = new List<ToeicQuestion>();

    public ICollection<ToeicAttempt> Attempts { get; set; } = new List<ToeicAttempt>();

    public bool IsDeleted => DeletedAt.HasValue;

    public bool IsPublished => Status == ContentStatus.Active && IsActive && !IsDeleted;

    public void MarkAsDeleted(DateTime deletedAt)
    {
        DeletedAt = deletedAt;
    }
}
