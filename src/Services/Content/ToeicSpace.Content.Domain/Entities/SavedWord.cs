namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// A word in a learner's library: either an official roadmap word or the learner's own custom word
/// (a private <see cref="VocabularyWord"/>). Unfiled when <see cref="FolderId"/> is null.
/// </summary>
public sealed class SavedWord
{
    public const int NoteMaxLength = 300;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public Guid WordId { get; set; }
    public VocabularyWord? Word { get; set; }
    public Guid? FolderId { get; set; }
    public VocabularyFolder? Folder { get; set; }
    public string? Note { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsCustom => Word?.OwnerKey is { } owner && owner != Guid.Empty;
}
