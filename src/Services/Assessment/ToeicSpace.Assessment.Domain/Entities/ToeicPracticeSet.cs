using ToeicSpace.Assessment.Domain.Common;
using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Domain.Entities;

/// <summary>
/// A practice collection for a single TOEIC part (by level, by topic or a mixed drill).
/// It references questions from the bank instead of owning copies of them.
/// </summary>
public class ToeicPracticeSet : BaseAuditableEntity
{
    public string Code { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public PracticeSetKind Kind { get; set; }

    public ToeicPart Part { get; set; }

    /// <summary>
    /// Difficulty level from 1 (basic) to 5 (expert). Only used by <see cref="PracticeSetKind.Level"/> sets.
    /// </summary>
    public int? Level { get; set; }

    /// <summary>
    /// Target TOEIC score of the set (e.g. 450, 550). Only used by <see cref="PracticeSetKind.Level"/> sets.
    /// </summary>
    public int? TargetScore { get; set; }

    public int DurationMinutes { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Draft;

    public int OrderIndex { get; set; }

    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// External ID mapped from source databases (e.g. Studychill test UUID).
    /// </summary>
    public Guid? ExternalId { get; set; }

    public string? Source { get; set; }

    public DateTime? DeletedAt { get; set; }

    public ICollection<ToeicPracticeSetItem> Items { get; set; } = new List<ToeicPracticeSetItem>();

    public bool IsDeleted => DeletedAt.HasValue;

    public void MarkAsDeleted(DateTime deletedAt)
    {
        DeletedAt = deletedAt;
    }
}
