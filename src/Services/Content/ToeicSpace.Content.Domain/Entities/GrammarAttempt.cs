namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One submission of a grammar block (quick check <c>qc-u</c>, mini test <c>mt</c> or stage test <c>stage</c>). The score
/// is fixed at submission; later in-place fixes are listed in <see cref="FixedQuestionIds"/> and never change it.
/// Stage-test progress (best score, passed, attempts) is computed from these rows.
/// </summary>
public sealed class GrammarAttempt
{
    public long Id { get; set; }
    public Guid LearnerId { get; set; }
    public string? Topic { get; set; }
    public string? Stage { get; set; }
    public string Block { get; set; } = string.Empty;
    /// <summary>Answers as submitted ({questionId: value}, raw JSON).</summary>
    public string Answers { get; set; } = "{}";
    /// <summary>Correctness at submission per question id.</summary>
    public Dictionary<string, bool> Results { get; set; } = [];
    public List<string> FixedQuestionIds { get; set; } = [];
    public int Score { get; set; }
    public int Total { get; set; }
    public DateTime CreatedAt { get; set; }
}
