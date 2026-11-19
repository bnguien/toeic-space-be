using System.Collections.Concurrent;
using System.Net;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.Application.Interfaces.Services.Enrichment;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;
using ToeicSpace.Content.Domain.Enums;

namespace ToeicSpace.Content.Tests.Vocabulary;

/// <summary>In-memory test doubles for every enrichment tier; no network.</summary>
public sealed class FakeTiers
{
    public FakeCache Cache { get; } = new();
    public FakeLexicon Lexicon { get; } = new();
    public FakeDictionary Dictionary { get; } = new();
    public FakeTranslation Translation { get; } = new();
    public FakeAi Ai { get; } = new();

    public WordEnricher Enricher(ICuratedLexicon? lexicon = null) => new(Cache, lexicon ?? Lexicon, Dictionary,
        Translation, Ai, NullLogger<WordEnricher>.Instance);
}

public sealed class FakeCache : IWordGlossCache
{
    public Dictionary<(string, WordKind), WordGloss> Entries { get; } = [];
    public List<WordGloss> Saved { get; } = [];

    public Task<WordGloss?> GetAsync(string normalizedText, WordKind kind, CancellationToken ct) =>
        Task.FromResult(Entries.GetValueOrDefault((normalizedText, kind)));

    public Task SetAsync(WordGloss gloss, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(gloss.MeaningVi)) Saved.Add(gloss);
        return Task.CompletedTask;
    }
}

public sealed class FakeLexicon : ICuratedLexicon
{
    public Dictionary<string, CuratedMatch> Words { get; } = [];
    public Task<CuratedMatch?> FindAsync(string normalizedText, PartOfSpeech? hint, CancellationToken ct) =>
        Task.FromResult(Words.GetValueOrDefault(normalizedText));
}

public sealed class FakeDictionary : IDictionaryProvider
{
    public Dictionary<string, WordGloss> Words { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentBag<string> Calls { get; } = [];
    public TimeSpan Delay { get; set; }

    public async Task<WordGloss?> LookupAsync(string word, CancellationToken ct)
    {
        Calls.Add(word);
        if (Delay > TimeSpan.Zero) await Task.Delay(Delay, ct);
        return Words.TryGetValue(word, out var gloss) ? Copy(gloss) : null;
    }

    private static WordGloss Copy(WordGloss g) => new()
    {
        Phonetic = g.Phonetic, PartOfSpeech = g.PartOfSpeech, DefinitionEn = g.DefinitionEn, Example = g.Example,
        MeaningVi = g.MeaningVi
    };
}

public sealed class FakeTranslation : ITranslationProvider
{
    public Dictionary<string, string> Texts { get; } = new(StringComparer.OrdinalIgnoreCase);
    public ConcurrentBag<string> Calls { get; } = [];
    public GlossSource Source { get; set; } = GlossSource.Google;

    public Task<TranslationResult?> TranslateAsync(string text, CancellationToken ct)
    {
        Calls.Add(text);
        return Task.FromResult(Texts.TryGetValue(text, out var vi) ? new TranslationResult(vi, Source) : null);
    }
}

public sealed class FakeAi : IAiWordProvider
{
    public bool IsConfigured { get; set; } = true;
    public Dictionary<string, WordGloss> Words { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, PhraseAnalysis> Phrases { get; } = new(StringComparer.OrdinalIgnoreCase);
    public List<int> BatchSizes { get; } = [];
    public List<string> PhraseCalls { get; } = [];
    public List<string> TranslateCalls { get; } = [];

    public Task<IReadOnlyDictionary<string, WordGloss>> FillWordsAsync(IReadOnlyList<AiWordRequest> words,
        bool interactive, CancellationToken ct)
    {
        BatchSizes.Add(words.Count);
        IReadOnlyDictionary<string, WordGloss> result = words.Where(w => Words.ContainsKey(w.Word))
            .ToDictionary(w => w.Word, w => Words[w.Word]);
        return Task.FromResult(result);
    }

    public Task<PhraseAnalysis?> AnalyzePhraseAsync(string phrase, string? context, bool interactive, CancellationToken ct)
    {
        PhraseCalls.Add(phrase);
        return Task.FromResult(Phrases.GetValueOrDefault(phrase));
    }

    public Task<string?> TranslateAsync(string text, string? context, bool interactive, CancellationToken ct)
    {
        TranslateCalls.Add(text);
        return Task.FromResult<string?>($"[ai] {text}");
    }
}

/// <summary>Scripted HTTP responses for provider tests; records every request.</summary>
public sealed class ScriptedHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];
    public List<string> Bodies { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        Requests.Add(request);
        Bodies.Add(request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(ct));
        return respond(request);
    }

    public static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) =>
        new(status) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
}
