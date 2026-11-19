namespace ToeicSpace.Content.Infrastructure.Services.Enrichment.Gemini;

/// <summary>
/// Section <c>Gemini</c>. Env: GEMINI_API_KEY → <c>Gemini__ApiKey</c>, GEMINI_API_KEYS (comma/newline list) →
/// <c>Gemini__ApiKeys</c>, DICT_AI_MODEL → <c>Gemini__InteractiveModel</c>, FLASHCARD_AI_MODEL → <c>Gemini__BatchModel</c>.
/// </summary>
public sealed class GeminiOptions
{
    public const string SectionName = "Gemini";
    public const string DefaultModel = "gemini-2.5-flash";

    public string? ApiKey { get; set; }
    public string? ApiKeys { get; set; }
    /// <summary>Model for requests a user is waiting on; defaults to <see cref="BatchModel"/>.</summary>
    public string? InteractiveModel { get; set; }
    public string? BatchModel { get; set; }
    public int InteractiveTimeoutSeconds { get; set; } = 8;
    public int BatchTimeoutSeconds { get; set; } = 25;
    public string BaseUrl { get; set; } = "https://generativelanguage.googleapis.com/v1beta/models/";

    public string ResolvedBatchModel => string.IsNullOrWhiteSpace(BatchModel) ? DefaultModel : BatchModel.Trim();
    public string ResolvedInteractiveModel =>
        string.IsNullOrWhiteSpace(InteractiveModel) ? ResolvedBatchModel : InteractiveModel.Trim();

    /// <summary><see cref="ApiKey"/> first (when not already listed), then <see cref="ApiKeys"/>, de-duplicated.</summary>
    public IReadOnlyList<string> ResolveKeys()
    {
        var keys = (ApiKeys ?? string.Empty).Split([',', ';', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
        if (!string.IsNullOrWhiteSpace(ApiKey) && !keys.Contains(ApiKey.Trim())) keys.Insert(0, ApiKey.Trim());
        return keys.Distinct().ToArray();
    }
}
