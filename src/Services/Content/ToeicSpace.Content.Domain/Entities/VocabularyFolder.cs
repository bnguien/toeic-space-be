namespace ToeicSpace.Content.Domain.Entities;

/// <summary>A learner's library folder; names are unique per learner, case-insensitive.</summary>
public sealed class VocabularyFolder
{
    public const int NameMaxLength = 60;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string NormalizedName { get; set; } = string.Empty;
    public int Order { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    /// <summary>Trimmed, lowercase, single-spaced form used for the per-learner unique index.</summary>
    public static string Normalize(string name) => VocabularyWord.Normalize(name);

    public void Rename(string name, DateTime now)
    {
        Name = name.Trim();
        NormalizedName = Normalize(name);
        UpdatedAt = now;
    }
}
