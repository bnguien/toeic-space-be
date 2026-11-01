using Microsoft.Extensions.Logging;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;

namespace ToeicSpace.Content.Application.Services.Enrichment;

/// <summary>How far the pipeline may go for one call.</summary>
/// <param name="AllowAi">Tier 5 may run.</param>
/// <param name="Interactive">Short AI timeout (user waiting) instead of the batch timeout.</param>
/// <param name="Deadline">Hard limit for the whole call; whatever is known by then is returned.</param>
/// <param name="FillAllBlanks">Run AI while any core field is blank, not only while the gloss is incomplete.</param>
public sealed record EnrichOptions(bool AllowAi, bool Interactive, TimeSpan Deadline, bool FillAllBlanks = false);

/// <summary>
/// The single enrichment pipeline used by lookup, import and AI fill. Tiers run in order and only fill blank fields:
/// 1 cache → 2 official catalog → 3 dictionary (single words) ∥ 4 translation → 5 AI (batched, ≤ 30 words per
/// prompt; phrases always get the phrase analysis). Stops early once a gloss is complete. Every field records its
/// source; fields passed in <see cref="EnrichRequest.Known"/> are never replaced and carry no source.
/// </summary>
public sealed class WordEnricher(IWordGlossCache cache, ICuratedLexicon lexicon, IDictionaryProvider dictionary,
    ITranslationProvider translation, IAiWordProvider ai, ILogger<WordEnricher> logger)
{
    public const int AiBatchSize = 30;
    private const int NetworkParallelism = 6;

    private static readonly GlossField[] AiFields =
        [GlossField.MeaningVi, GlossField.Phonetic, GlossField.PartOfSpeech, GlossField.Example, GlossField.ExampleVi];

    public async Task<EnrichResult> EnrichAsync(EnrichRequest request, EnrichOptions options, CancellationToken ct) =>
        (await EnrichManyAsync([request], options, ct))[0];

    public async Task<IReadOnlyList<EnrichResult>> EnrichManyAsync(IReadOnlyList<EnrichRequest> requests,
        EnrichOptions options, CancellationToken ct)
    {
        var items = requests.Select(Item.Create).ToArray();
        var work = items.Where(x => x.Input.Kind != WordKind.Reject).ToArray();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
        deadline.CancelAfter(options.Deadline);
        var token = deadline.Token;

        if (await RunTierAsync(() => CacheAndCatalogAsync(work, token), ct))
        {
            var pending = work.Where(x => !x.Gloss.IsComplete).ToArray();
            if (await RunTierAsync(() => DictionaryAndTranslationAsync(pending, token), ct) && options.AllowAi
                && ai.IsConfigured)
                await RunTierAsync(() => AiAsync(work, options, token), ct);
        }
        else logger.LogInformation("Enrichment deadline reached in the cache/catalog tier");

        foreach (var item in work.Where(x => x.Gloss.Sources.Values.Any(s => s != GlossSource.Cache)))
            await cache.SetAsync(ToCacheable(item.Gloss), ct);

        return items.Select(x => new EnrichResult(x.Gloss, x.Input.Kind != WordKind.Reject && x.Gloss.IsComplete,
            x.AiUsed, x.OfficialWordId, x.Gloss.Sources.Keys.ToArray())).ToArray();
    }

    /// <summary>Runs a tier; a deadline hit (not the caller's cancellation) stops the pipeline but keeps results.</summary>
    private static async Task<bool> RunTierAsync(Func<Task> tier, CancellationToken callerToken)
    {
        try
        {
            await tier();
            return true;
        }
        catch (OperationCanceledException) when (!callerToken.IsCancellationRequested)
        {
            return false;
        }
    }

    // Tiers 1–2 use the request's database context, so they run one item at a time.
    private async Task CacheAndCatalogAsync(Item[] items, CancellationToken ct)
    {
        foreach (var item in items)
        {
            item.Gloss.Fill(await cache.GetAsync(item.Input.Key, item.Input.Kind, ct), GlossSource.Cache);
            var match = await lexicon.FindAsync(item.Input.Key, item.Request.PartOfSpeechHint, ct);
            if (match is null) continue;
            item.OfficialWordId = match.WordId;
            if (!item.Gloss.IsComplete) item.Gloss.Fill(match.Gloss, GlossSource.Catalog);
        }
    }

    // Tiers 3–4 are HTTP only: items run in parallel, dictionary and translation of one item too.
    private async Task DictionaryAndTranslationAsync(Item[] items, CancellationToken ct)
    {
        using var gate = new SemaphoreSlim(NetworkParallelism);
        await Task.WhenAll(items.Select(async item =>
        {
            await gate.WaitAsync(ct);
            try { await DictionaryAndTranslationAsync(item, ct); }
            finally { gate.Release(); }
        }));
    }

    private async Task DictionaryAndTranslationAsync(Item item, CancellationToken ct)
    {
        var gloss = item.Gloss;
        var dictionaryTask = item.Input.Kind == WordKind.Word
            ? dictionary.LookupAsync(item.Input.Text, ct) : Task.FromResult<WordGloss?>(null);
        var translationTask = gloss.Has(GlossField.MeaningVi)
            ? Task.FromResult<TranslationResult?>(null) : translation.TranslateAsync(item.Input.Text, ct);
        await Task.WhenAll(dictionaryTask, translationTask);

        var fromDictionary = await dictionaryTask;
        if (fromDictionary is not null)
        {
            // A dictionary never provides the Vietnamese meaning; its English definition stays in definitionEn.
            fromDictionary.MeaningVi = null;
            gloss.Fill(fromDictionary, GlossSource.Dictionary);
        }
        if (await translationTask is { } meaning) gloss.FillText(GlossField.MeaningVi, meaning.Text, meaning.Source);
        if (gloss.Has(GlossField.Example) && !gloss.Has(GlossField.ExampleVi)
            && await translation.TranslateAsync(gloss.Example!, ct) is { } exampleVi)
            gloss.FillText(GlossField.ExampleVi, exampleVi.Text, exampleVi.Source);
    }

    private async Task AiAsync(Item[] items, EnrichOptions options, CancellationToken ct)
    {
        var words = items.Where(x => x.Input.Kind == WordKind.Word && NeedsAi(x.Gloss, options)).ToArray();
        foreach (var batch in words.Chunk(AiBatchSize))
        {
            var results = await ai.FillWordsAsync(batch.Select(x => new AiWordRequest(x.Input.Text,
                x.Gloss.PartOfSpeech, x.Gloss.MeaningVi)).ToArray(), options.Interactive, ct);
            foreach (var item in batch)
            {
                item.AiUsed = true;
                if (results.TryGetValue(item.Input.Text, out var gloss)
                    || results.TryGetValue(item.Input.Key, out gloss))
                    item.Gloss.Fill(gloss, GlossSource.Ai);
            }
        }

        foreach (var item in items.Where(x => x.Input.Kind == WordKind.Phrase
                     && (!x.Gloss.Has(GlossField.PhraseType) || NeedsAi(x.Gloss, options))))
        {
            item.AiUsed = true;
            var analysis = await ai.AnalyzePhraseAsync(item.Input.Text, item.Request.Context, options.Interactive, ct);
            if (analysis is null) continue;
            item.Gloss.Fill(new WordGloss
            {
                MeaningVi = analysis.MeaningVi, PhraseType = analysis.PhraseType, Usage = analysis.Usage,
                Explanation = analysis.Explanation, LiteralMeaning = analysis.LiteralMeaning,
                Example = analysis.Example, ExampleVi = analysis.ExampleVi
            }, GlossSource.Ai);
        }

        foreach (var item in items.Where(x => x.AiUsed && x.Gloss.Has(GlossField.Example)
                     && !x.Gloss.Has(GlossField.ExampleVi)))
            item.Gloss.FillText(GlossField.ExampleVi,
                await ai.TranslateAsync(item.Gloss.Example!, item.Input.Text, options.Interactive, ct), GlossSource.Ai);
    }

    private static bool NeedsAi(WordGloss gloss, EnrichOptions options) =>
        options.FillAllBlanks ? AiFields.Any(f => !gloss.Has(f)) : !gloss.IsComplete;

    /// <summary>Only tier-produced fields are cached; fields the user typed are never shared.</summary>
    private static WordGloss ToCacheable(WordGloss gloss)
    {
        var copy = new WordGloss
        {
            Text = gloss.Text, NormalizedText = gloss.NormalizedText, Kind = gloss.Kind, Headword = gloss.Headword
        };
        foreach (var (field, source) in gloss.Sources)
        {
            if (field == GlossField.PartOfSpeech) copy.FillPartOfSpeech(gloss.PartOfSpeech, source);
            else copy.FillText(field, gloss.GetText(field), source);
        }
        return copy;
    }

    private sealed class Item
    {
        public required EnrichRequest Request { get; init; }
        public required NormalizedInput Input { get; init; }
        public required WordGloss Gloss { get; init; }
        public Guid? OfficialWordId { get; set; }
        public bool AiUsed { get; set; }

        public static Item Create(EnrichRequest request)
        {
            var input = InputNormalizer.Normalize(request.Text);
            var gloss = new WordGloss
            {
                Text = input.Text, NormalizedText = input.Key, Kind = input.Kind, Headword = input.Text
            };
            if (request.Known is { } known)
            {
                // Known fields are copied without provenance, then the source entries are dropped again.
                gloss.Fill(known, GlossSource.Cache);
                gloss.Sources.Clear();
            }
            return new Item { Request = request, Input = input, Gloss = gloss };
        }
    }
}
