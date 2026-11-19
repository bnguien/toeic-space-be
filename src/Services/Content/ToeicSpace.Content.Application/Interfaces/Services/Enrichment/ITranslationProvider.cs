using ToeicSpace.Content.Application.Models.Enrichment;

namespace ToeicSpace.Content.Application.Interfaces.Services.Enrichment;

/// <summary>Tier 4: English → Vietnamese machine translation chain with junk/echo filtering.</summary>
public interface ITranslationProvider
{
    /// <summary>Null when every provider failed or returned junk; never throws except on cancellation.</summary>
    Task<TranslationResult?> TranslateAsync(string text, CancellationToken ct);
}
