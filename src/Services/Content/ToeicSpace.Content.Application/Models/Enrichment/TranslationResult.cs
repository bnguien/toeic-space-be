namespace ToeicSpace.Content.Application.Models.Enrichment;

/// <summary>A Vietnamese translation and the provider tier that produced it (Google or MyMemory).</summary>
public sealed record TranslationResult(string Text, GlossSource Source);
