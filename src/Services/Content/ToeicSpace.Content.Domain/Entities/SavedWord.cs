using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// A word in a learner's library: either an official roadmap word or the learner's own custom word
/// (a private <see cref="VocabularyWord"/>). Unfiled when <see cref="FolderId"/> is null.
/// For an official word the <c>My*</c> columns hold the learner's personal edits, overlaid on the shared word when
/// read (null = the shared value); the shared word itself is never changed.
/// </summary>
public sealed class SavedWord
{
    public const int NoteMaxLength = 1000;
    public const int MyPhoneticMaxLength = 100;
    public const int MyMeaningMaxLength = 200;
    public const int MyTextMaxLength = 300;

    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LearnerId { get; set; }
    public Guid WordId { get; set; }
    public VocabularyWord? Word { get; set; }
    public Guid? FolderId { get; set; }
    public VocabularyFolder? Folder { get; set; }
    public string? Note { get; set; }
    public string? MyPhonetic { get; set; }
    public PartOfSpeech? MyPartOfSpeech { get; set; }
    public string? MyMeaningVi { get; set; }
    public string? MyExample { get; set; }
    public string? MyExampleVi { get; set; }
    /// <summary>Last change of the personal edits; null while there are none.</summary>
    public DateTime? PersonalizedAt { get; set; }
    public DateTime SavedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsCustom => Word?.OwnerKey is { } owner && owner != Guid.Empty;

    public bool HasPersonalEdits => MyPhonetic is not null || MyPartOfSpeech is not null || MyMeaningVi is not null
        || MyExample is not null || MyExampleVi is not null;

    /// <summary>
    /// The override to store for a field: a blank value or one equal to the shared value (trimmed, case-sensitive)
    /// clears it, so "back to the original" only needs an empty field or the original text.
    /// </summary>
    public static string? PersonalValue(string? value, string? shared)
    {
        var trimmed = value?.Trim();
        return string.IsNullOrEmpty(trimmed) || trimmed == (shared?.Trim() ?? string.Empty) ? null : trimmed;
    }

    /// <summary>Clears every personal edit (the note is kept).</summary>
    public void ResetPersonal()
    {
        MyPhonetic = null;
        MyPartOfSpeech = null;
        MyMeaningVi = null;
        MyExample = null;
        MyExampleVi = null;
    }

    /// <summary>Call after changing personal edits: keeps <see cref="PersonalizedAt"/> in step with them.</summary>
    public void StampPersonalized(DateTime now) => PersonalizedAt = HasPersonalEdits ? now : null;
}
