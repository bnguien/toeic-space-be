using ToeicSpace.Assessment.Domain.Common;
using ToeicSpace.Assessment.Domain.Enums;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Domain.Entities;

/// <summary>
/// A question in the question bank. It belongs to at most one full test (TestId + QuestionNumber)
/// and can be reused by any number of practice sets.
/// </summary>
public class ToeicQuestion : BaseAuditableEntity
{
    private ToeicPart _part;

    public Guid? TestId { get; set; }

    public ToeicTest? Test { get; set; }

    public Guid? PassageId { get; set; }

    public ToeicPassage? Passage { get; set; }

    /// <summary>
    /// Setting the part also keeps <see cref="Section"/> consistent with the ETS format.
    /// </summary>
    public ToeicPart Part
    {
        get => _part;
        set
        {
            _part = value;
            Section = ToeicPartRules.GetSection(value);
        }
    }

    public ToeicSection Section { get; private set; }

    /// <summary>
    /// Question number inside the owning full test (1-200). Null for bank-only questions.
    /// </summary>
    public int? QuestionNumber { get; set; }

    /// <summary>
    /// Question text. Null for Part 1 &amp; 2 where questions are delivered via audio.
    /// </summary>
    public string? QuestionText { get; set; }

    public string? AudioUrl { get; set; }

    public string? ImageUrl { get; set; }

    public string OptionA { get; set; } = string.Empty;

    public string OptionB { get; set; } = string.Empty;

    public string OptionC { get; set; } = string.Empty;

    /// <summary>
    /// Option D is null for Part 2 questions (only 3 options A, B, C).
    /// </summary>
    public string? OptionD { get; set; }

    public AnswerOption CorrectAnswer { get; set; }

    public string? Explanation { get; set; }

    public string? Transcript { get; set; }

    public QuestionDifficulty DifficultyLevel { get; set; } = QuestionDifficulty.Medium;

    public string? Topic { get; set; }

    public ContentStatus Status { get; set; } = ContentStatus.Active;

    /// <summary>
    /// Revision number, also used as an optimistic concurrency token.
    /// </summary>
    public int Version { get; set; } = 1;

    public int OrderIndex { get; set; }

    public bool PreferAiExplanation { get; set; }

    public Guid? CreatedByUserId { get; set; }

    /// <summary>
    /// External ID mapped from source databases (e.g. Studychill question UUID).
    /// </summary>
    public Guid? ExternalId { get; set; }

    public DateTime? DeletedAt { get; set; }

    public ICollection<ToeicPracticeSetItem> PracticeSetItems { get; set; } = new List<ToeicPracticeSetItem>();

    public bool IsDeleted => DeletedAt.HasValue;

    public void MarkAsDeleted(DateTime deletedAt)
    {
        DeletedAt = deletedAt;
    }

    public void IncreaseVersion()
    {
        Version++;
    }
}
