namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One course of a dictation pack: a stage of the TOEIC roadmap (packs toeic-250 … toeic-990) or a lesson of a
/// topic pack (travel, office…). Summary numbers are computed at import so maps never read sentence content.
/// </summary>
public sealed class DictationCourse
{
    public int Id { get; set; }
    /// <summary>Natural key from the source data, e.g. <c>sc_crs_t250_001</c>.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>English pack key, e.g. <c>toeic-250</c>, <c>travel</c>.</summary>
    public string Pack { get; set; } = string.Empty;
    public int Level { get; set; }
    /// <summary>Position inside the pack.</summary>
    public int Order { get; set; }
    public string Title { get; set; } = string.Empty;
    /// <summary>Active sentences.</summary>
    public int ItemCount { get; set; }
    public double WordsPerSentence { get; set; }
    /// <summary>Words to pre-learn in basic mode.</summary>
    public int VocabCount { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<DictationItem> Items { get; set; } = [];
}
