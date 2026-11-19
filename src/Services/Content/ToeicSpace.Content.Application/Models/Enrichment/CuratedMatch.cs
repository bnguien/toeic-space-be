namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>An official roadmap word that matches the input exactly (normalized headword).</summary>
public sealed record CuratedMatch(Guid WordId, WordGloss Gloss);
