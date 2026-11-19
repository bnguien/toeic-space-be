using ToeicSpace.Content.Application.Models.Enrichment;

namespace ToeicSpace.Content.Application.Interfaces.Services.Enrichment;

/// <summary>Tier 1: persisted gloss cache (TTL, version). Writes never join the caller's unit of work.</summary>
public interface IWordGlossCache
{
    Task<WordGloss?> GetAsync(string normalizedText, WordKind kind, CancellationToken ct);
    /// <summary>Stores a gloss that has a Vietnamese meaning; other glosses are ignored.</summary>
    Task SetAsync(WordGloss gloss, CancellationToken ct);
}
