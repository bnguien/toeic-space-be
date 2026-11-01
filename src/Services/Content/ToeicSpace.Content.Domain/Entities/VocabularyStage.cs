namespace ToeicSpace.Content.Domain.Entities;

/// <summary>One station of the TOEIC vocabulary roadmap (natural key: <see cref="Slug"/>).</summary>
public sealed class VocabularyStage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Slug { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Order { get; set; }
    public int? ToeicTarget { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<VocabularySet> Sets { get; set; } = [];
}
