using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment;

/// <summary>
/// Tier 3: dictionaryapi.dev (single words). Phonetic, first part of speech, first two definitions as
/// "(pos) def" joined by " · " (≤ 320) and the first example (≤ 240). 2 s timeout; circuit breaker.
/// </summary>
public sealed class FreeDictionaryProvider(HttpClient http, DictionaryCircuitBreaker breaker,
    ILogger<FreeDictionaryProvider> logger) : IDictionaryProvider
{
    public const string BaseUrl = "https://api.dictionaryapi.dev/api/v2/entries/en/";
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(2);

    public async Task<WordGloss?> LookupAsync(string word, CancellationToken ct)
    {
        if (breaker.IsOpen || string.IsNullOrWhiteSpace(word) || word.Contains(' ')) return null;
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        timeout.CancelAfter(Timeout);
        try
        {
            using var response = await http.GetAsync(BaseUrl + Uri.EscapeDataString(word.Trim()), timeout.Token);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                breaker.RecordSuccess();
                return null;
            }
            response.EnsureSuccessStatusCode();
            await using var stream = await response.Content.ReadAsStreamAsync(timeout.Token);
            using var document = await JsonDocument.ParseAsync(stream, cancellationToken: timeout.Token);
            breaker.RecordSuccess();
            return Parse(document.RootElement);
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException
                                       || ex is OperationCanceledException && !ct.IsCancellationRequested)
        {
            if (breaker.RecordFailure())
                logger.LogWarning("Dictionary circuit opened for {Minutes} minutes after repeated failures",
                    DictionaryCircuitBreaker.OpenFor.TotalMinutes);
            return null;
        }
    }

    public static WordGloss? Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Array) return null;
        var gloss = new WordGloss();
        var definitions = new List<string>();
        foreach (var entry in root.EnumerateArray())
        {
            if (gloss.Phonetic is null)
                gloss.Phonetic = NonEmpty(entry, "phonetic") ?? (entry.TryGetProperty("phonetics", out var phonetics)
                    && phonetics.ValueKind == JsonValueKind.Array
                        ? phonetics.EnumerateArray().Select(p => NonEmpty(p, "text")).FirstOrDefault(t => t is not null)
                        : null);
            if (!entry.TryGetProperty("meanings", out var meanings) || meanings.ValueKind != JsonValueKind.Array) continue;
            foreach (var meaning in meanings.EnumerateArray())
            {
                var pos = NonEmpty(meaning, "partOfSpeech");
                gloss.PartOfSpeech ??= PosNormalizer.Normalize(pos);
                if (!meaning.TryGetProperty("definitions", out var defs) || defs.ValueKind != JsonValueKind.Array) continue;
                foreach (var def in defs.EnumerateArray())
                {
                    if (NonEmpty(def, "definition") is { } text && definitions.Count < 2)
                        definitions.Add(pos is null ? text : $"({pos}) {text}");
                    gloss.Example ??= Cut(NonEmpty(def, "example"), 240);
                }
            }
        }
        if (definitions.Count > 0) gloss.DefinitionEn = Cut(string.Join(" · ", definitions), 320);
        return gloss.Phonetic is null && gloss.PartOfSpeech is null && gloss.DefinitionEn is null ? null : gloss;
    }

    private static string? NonEmpty(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
        && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim() : null;

    private static string? Cut(string? value, int max) =>
        value is null || value.Length <= max ? value : value[..(max - 1)].TrimEnd() + "…";
}
