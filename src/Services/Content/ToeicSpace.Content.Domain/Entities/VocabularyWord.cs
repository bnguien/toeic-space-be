using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// Word bank entry. Official roadmap words have <see cref="OwnerKey"/> = <see cref="Guid.Empty"/> and are unique by
/// (normalized headword, part of speech); a learner's custom words carry the learner id as owner and are unique per
/// owner, so progress, scheduling and the study modes treat both kinds the same way.
/// </summary>
public sealed class VocabularyWord
{
    public const int CustomHeadwordMaxLength = 80;
    public const int CustomMeaningMaxLength = 200;
    public const int CustomTextMaxLength = 300;

    public Guid Id { get; set; } = Guid.NewGuid();
    /// <summary><see cref="Guid.Empty"/> for official words, otherwise the owning learner.</summary>
    public Guid OwnerKey { get; set; } = Guid.Empty;
    public string Headword { get; set; } = string.Empty;
    public string NormalizedHeadword { get; set; } = string.Empty;
    public PartOfSpeech PartOfSpeech { get; set; }
    public string Phonetic { get; set; } = string.Empty;
    public string MeaningVi { get; set; } = string.Empty;
    public string? DefinitionEn { get; set; }
    public string? Example { get; set; }
    public string? ExampleVi { get; set; }
    public string? Cefr { get; set; }
    public List<string> Synonyms { get; set; } = [];
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public bool IsCustom => OwnerKey != Guid.Empty;
    public bool IsAccessibleBy(Guid learnerId) => OwnerKey == Guid.Empty || OwnerKey == learnerId;

    /// <summary>Lowercase, trimmed, single-spaced form used for de-duplication.</summary>
    public static string Normalize(string headword) =>
        string.Join(' ', headword.Trim().ToLowerInvariant()
            .Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
}
