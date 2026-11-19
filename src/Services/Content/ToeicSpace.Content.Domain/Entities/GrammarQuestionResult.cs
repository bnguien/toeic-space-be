namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// A learner × question record: first answer, last answer, fixed after a wrong first answer, answer revealed before
/// being right, number of wrong answers. Source of the mistake book ("Sổ lỗi") and of the frequent traps.
/// </summary>
public sealed class GrammarQuestionResult
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public string QuestionId { get; set; } = string.Empty;
    /// <summary>Topic of the question's main unit (a review question in topic 12 may belong to topic 05).</summary>
    public string Topic { get; set; } = string.Empty;
    public string Block { get; set; } = string.Empty;
    public string UnitCode { get; set; } = string.Empty;
    public string TrapCode { get; set; } = string.Empty;
    /// <summary>null = never answered (only revealed).</summary>
    public bool? FirstCorrect { get; set; }
    public bool LastCorrect { get; set; }
    public bool Fixed { get; set; }
    public bool Revealed { get; set; }
    public int WrongCount { get; set; }
    public DateTime? AnsweredAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Records one graded, non-blank answer (blank answers never reach this).</summary>
    public void Record(bool correct, DateTime now)
    {
        if (FirstCorrect is null) FirstCorrect = correct;
        else if (correct && FirstCorrect == false && !Fixed) Fixed = true;
        if (!correct) WrongCount++;
        LastCorrect = correct;
        AnsweredAt = now;
        UpdatedAt = now;
    }

    /// <summary>Marks the answer as seen unless the learner already got the question right.</summary>
    public void MarkRevealed(DateTime now)
    {
        if (Revealed || FirstCorrect == true || Fixed) return;
        Revealed = true;
        UpdatedAt = now;
    }
}
