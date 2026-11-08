using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Domain.Entities;

/// <summary>
/// Cached enrichment result of a word or phrase (unique by normalized text + kind). Only entries with a Vietnamese
/// meaning are stored; an entry from another <see cref="Version"/> or past <see cref="ExpiresAt"/> is ignored.
/// </summary>
public sealed class WordGlossCacheEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string NormalizedText { get; set; } = string.Empty;
    /// <summary><c>word</c> or <c>phrase</c>.</summary>
    public string Kind { get; set; } = string.Empty;
    public string Headword { get; set; } = string.Empty;
    public string? Phonetic { get; set; }
    public PartOfSpeech? PartOfSpeech { get; set; }
    public string MeaningVi { get; set; } = string.Empty;
    public string? DefinitionEn { get; set; }
    public string? Example { get; set; }
    public string? ExampleVi { get; set; }
    public string? Cefr { get; set; }
    public string? PhraseType { get; set; }
    public string? Usage { get; set; }
    public string? Explanation { get; set; }
    public string? LiteralMeaning { get; set; }
    /// <summary>Field name → source that produced it (provenance).</summary>
    public Dictionary<string, string> Sources { get; set; } = [];
    public int Version { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
