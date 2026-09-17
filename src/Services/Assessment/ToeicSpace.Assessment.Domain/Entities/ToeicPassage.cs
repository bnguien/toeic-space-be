using ToeicSpace.Assessment.Domain.Common;
using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Domain.Entities;

/// <summary>
/// A question group: Part 3 conversation, Part 4 talk, Part 6/7 text. Belongs to a full test
/// or, when TestId is null, to the practice question bank.
/// </summary>
public class ToeicPassage : BaseAuditableEntity
{
    public Guid? TestId { get; set; }

    public ToeicTest? Test { get; set; }

    public ToeicPart Part { get; set; }

    public string? PassageType { get; set; }

    public string? Title { get; set; }

    public string? Content { get; set; }

    public string? AudioUrl { get; set; }

    public string? ImageUrl { get; set; }

    public string? Transcript { get; set; }

    public int OrderIndex { get; set; }

    /// <summary>
    /// External ID mapped from source databases (e.g. Studychill passage UUID).
    /// </summary>
    public Guid? ExternalId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public ICollection<ToeicQuestion> Questions { get; set; } = new List<ToeicQuestion>();

    public bool IsDeleted => DeletedAt.HasValue;

    public void MarkAsDeleted(DateTime deletedAt)
    {
        DeletedAt = deletedAt;
    }
}
