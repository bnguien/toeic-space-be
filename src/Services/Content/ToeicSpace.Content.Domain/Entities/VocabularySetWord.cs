namespace ToeicSpace.Content.Domain.Entities;

/// <summary>Membership of a word in a set, with its 1-based position.</summary>
public sealed class VocabularySetWord
{
    public Guid SetId { get; set; }
    public VocabularySet? Set { get; set; }
    public Guid WordId { get; set; }
    public VocabularyWord? Word { get; set; }
    public int Order { get; set; }
}
