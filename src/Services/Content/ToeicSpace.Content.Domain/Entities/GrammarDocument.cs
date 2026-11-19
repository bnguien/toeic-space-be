using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// One grammar content document stored as English JSON: the knowledge, vocabulary or exercises of a topic
/// (<see cref="Key"/> = "01".."44") or the test of a stage ("01".."05"). Natural key (Kind, Key); answers included —
/// the public payload is derived from it with every answer field stripped.
/// </summary>
public sealed class GrammarDocument
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public GrammarDocumentKind Kind { get; set; }
    public string Key { get; set; } = string.Empty;
    public string Content { get; set; } = "{}";
    /// <summary>SHA-256 (hex) of <see cref="Content"/>; same hash = unchanged document on re-import.</summary>
    public string ContentHash { get; set; } = string.Empty;
    public DateTime ImportedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
