using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Application.Interfaces.Services.Enrichment;

/// <summary>Tier 2: official roadmap words (exact normalized headword, preferring the part-of-speech hint).</summary>
public interface ICuratedLexicon
{
    Task<CuratedMatch?> FindAsync(string normalizedText, PartOfSpeech? hint, CancellationToken ct);
}
