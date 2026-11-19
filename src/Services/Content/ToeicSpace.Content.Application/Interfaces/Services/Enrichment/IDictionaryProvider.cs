using ToeicSpace.Content.Application.Models.Enrichment;

namespace ToeicSpace.Content.Application.Interfaces.Services.Enrichment;

/// <summary>Tier 3: English dictionary for single words (phonetic, part of speech, definition, example).</summary>
public interface IDictionaryProvider
{
    /// <summary>Null when unknown, failed or the circuit is open; never throws except on cancellation.</summary>
    Task<WordGloss?> LookupAsync(string word, CancellationToken ct);
}
