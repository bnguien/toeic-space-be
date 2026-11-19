namespace ToeicSpace.Content.Domain.Entities;

/// <summary>A numbered lesson of up to 50 words inside a stage (natural key: stage + <see cref="Slug"/>).</summary>
public sealed class VocabularySet
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid StageId { get; set; }
    public VocabularyStage? Stage { get; set; }
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Topic { get; set; }
    public int Order { get; set; }
    /// <summary>False when a later import no longer contains the set; it is hidden, never deleted.</summary>
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<VocabularySetWord> Words { get; set; } = [];
}
