namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>
/// AI analysis of a 2–5 word expression: <paramref name="PhraseType"/> is idiom | phrasal_verb | collocation | fixed |
/// literal; <paramref name="LiteralMeaning"/> warns about the word-by-word trap.
/// </summary>
public sealed record PhraseAnalysis(string? MeaningVi, string? PhraseType, string? Usage, string? Explanation,
    string? LiteralMeaning, string? Example, string? ExampleVi);
