using ToeicSpace.Content.Application.Models.Enrichment;

namespace ToeicSpace.Content.Application.Interfaces.Services.Enrichment;

/// <summary>Tier 5: generative AI. Every method returns empty/null on failure and never throws except on cancellation.</summary>
public interface IAiWordProvider
{
    bool IsConfigured { get; }

    /// <summary>Fills meaning, phonetic, part of speech, example and its translation; keyed by the input word.</summary>
    Task<IReadOnlyDictionary<string, WordGloss>> FillWordsAsync(IReadOnlyList<AiWordRequest> words, bool interactive,
        CancellationToken ct);

    Task<PhraseAnalysis?> AnalyzePhraseAsync(string phrase, string? context, bool interactive, CancellationToken ct);

    Task<string?> TranslateAsync(string text, string? context, bool interactive, CancellationToken ct);
}
