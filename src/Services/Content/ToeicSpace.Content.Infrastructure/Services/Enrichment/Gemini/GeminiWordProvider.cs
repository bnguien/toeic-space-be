using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;

namespace ToeicSpace.Content.Infrastructure.Services.Enrichment.Gemini;

/// <summary>Tier 5 over Gemini: word-batch fill, phrase analysis and translation prompts (embedded resources).</summary>
public sealed class GeminiWordProvider(GeminiClient client, IOptions<GeminiOptions> options,
    ILogger<GeminiWordProvider> logger) : IAiWordProvider
{
    public const int MaxBatch = 30;
    private static readonly Lazy<PromptTemplate> WordBatch = new(() => PromptTemplate.Load("word-batch"));
    private static readonly Lazy<PromptTemplate> Phrase = new(() => PromptTemplate.Load("phrase-analysis"));
    private static readonly Lazy<PromptTemplate> Translation = new(() => PromptTemplate.Load("translation"));
    private static readonly HashSet<string> PhraseTypes = ["idiom", "phrasal_verb", "collocation", "fixed", "literal"];

    public bool IsConfigured => client.IsConfigured;

    private string Model(bool interactive) =>
        interactive ? options.Value.ResolvedInteractiveModel : options.Value.ResolvedBatchModel;

    public async Task<IReadOnlyDictionary<string, WordGloss>> FillWordsAsync(IReadOnlyList<AiWordRequest> words,
        bool interactive, CancellationToken ct)
    {
        var result = new Dictionary<string, WordGloss>(StringComparer.OrdinalIgnoreCase);
        foreach (var batch in words.Chunk(MaxBatch))
        {
            var input = JsonSerializer.Serialize(batch.Select(w => new
            {
                word = w.Word, partOfSpeech = w.PartOfSpeech?.ToString().ToLowerInvariant(), meaningVi = w.MeaningVi
            }));
            var json = await client.GenerateJsonAsync(Model(interactive), WordBatch.Value.System,
                WordBatch.Value.Render(new Dictionary<string, string> { ["words"] = input }), interactive, ct);
            if (json is null) logger.LogInformation("Gemini returned no result for a batch of {Count} words", batch.Length);
            foreach (var (word, gloss) in ParseWordBatch(json))
                if (batch.Any(b => string.Equals(b.Word, word, StringComparison.OrdinalIgnoreCase)))
                    result[word] = gloss;
        }
        return result;
    }

    public async Task<PhraseAnalysis?> AnalyzePhraseAsync(string phrase, string? context, bool interactive,
        CancellationToken ct)
    {
        var json = await client.GenerateJsonAsync(Model(interactive), Phrase.Value.System,
            Phrase.Value.Render(Values(phrase, context)), interactive, ct);
        return ParsePhrase(json);
    }

    public async Task<string?> TranslateAsync(string text, string? context, bool interactive, CancellationToken ct)
    {
        var json = await client.GenerateJsonAsync(Model(interactive), Translation.Value.System,
            Translation.Value.Render(Values(text, context)), interactive, ct);
        var translation = Read(json, doc => Str(doc, "translation", 250));
        return TranslationFilter.IsJunk(text, translation) ? null : translation;
    }

    private static Dictionary<string, string> Values(string text, string? context) => new()
    {
        ["text"] = JsonSerializer.Serialize(text), ["context"] = JsonSerializer.Serialize(context ?? string.Empty)
    };

    /// <summary>Parses { "word": { meaningVi, phonetic, partOfSpeech, example, exampleVi } } with length limits.</summary>
    public static IReadOnlyDictionary<string, WordGloss> ParseWordBatch(string? json) =>
        Read(json, root =>
        {
            var result = new Dictionary<string, WordGloss>(StringComparer.OrdinalIgnoreCase);
            if (root.ValueKind != JsonValueKind.Object) return result;
            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind != JsonValueKind.Object) continue;
                var v = property.Value;
                result[property.Name] = new WordGloss
                {
                    MeaningVi = Str(v, "meaningVi", 200),
                    Phonetic = Ipa(Str(v, "phonetic", 60)),
                    PartOfSpeech = PosNormalizer.Normalize(Str(v, "partOfSpeech", 32)),
                    Example = Str(v, "example", 250),
                    ExampleVi = Str(v, "exampleVi", 250)
                };
            }
            return result;
        }) ?? new Dictionary<string, WordGloss>();

    public static PhraseAnalysis? ParsePhrase(string? json) => Read(json, root =>
    {
        var type = Str(root, "type", 32)?.ToLowerInvariant().Replace(' ', '_');
        var analysis = new PhraseAnalysis(Str(root, "meaningVi", 200), type is not null && PhraseTypes.Contains(type) ? type : null,
            Str(root, "usage", 250), Str(root, "explanation", 400), Str(root, "literalMeaning", 250),
            Str(root, "example", 250), Str(root, "exampleVi", 250));
        return analysis.MeaningVi is null && analysis.PhraseType is null ? null : analysis;
    });

    private static T? Read<T>(string? json, Func<JsonElement, T> read)
    {
        if (string.IsNullOrWhiteSpace(json)) return default;
        try
        {
            using var document = JsonDocument.Parse(json);
            return read(document.RootElement);
        }
        catch (JsonException)
        {
            return default;
        }
    }

    private static string? Str(JsonElement element, string name, int max)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var value)
            || value.ValueKind != JsonValueKind.String) return null;
        var text = value.GetString()?.Trim();
        if (string.IsNullOrEmpty(text)) return null;
        return text.Length <= max ? text : text[..max].TrimEnd();
    }

    private static string? Ipa(string? phonetic)
    {
        if (phonetic is null) return null;
        var core = phonetic.Trim().Trim('/', '[', ']').Trim();
        return core.Length == 0 ? null : $"/{core}/";
    }
}
