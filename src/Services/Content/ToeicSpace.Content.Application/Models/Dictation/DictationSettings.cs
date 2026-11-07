namespace ToeicSpace.Content.Application.Models.Dictation;

/// <summary>Configuration section <c>Dictation</c>.</summary>
public sealed class DictationSettings
{
    public const string SectionName = "Dictation";

    /// <summary>Secret of the word-proof salts (empty = a random secret per process).</summary>
    public string ProofSecret { get; set; } = string.Empty;
    public int ProofIterations { get; set; } = ToeicSpace.Content.Domain.Rules.WordProof.DefaultIterations;
    /// <summary>Next sentences whose audio is rendered in the background when a sentence opens (0 = off, max 8).</summary>
    public int PrewarmAhead { get; set; } = 4;
}
