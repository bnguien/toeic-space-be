using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Infrastructure.Services.Enrichment;
using ToeicSpace.Content.Infrastructure.Services.Enrichment.Gemini;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class GeminiKeyPoolTests
{
    private const string Model = "gemini-2.5-flash";
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));

    private GeminiKeyPool Pool(string? key = "k0", string? keys = "k1, k2\nk0") =>
        new(Options.Create(new GeminiOptions { ApiKey = key, ApiKeys = keys }), _clock, NullLogger<GeminiKeyPool>.Instance);

    [Fact]
    public void Keys_merge_the_single_key_first_and_rotate_round_robin()
    {
        // ApiKey is prepended only when the list does not already contain it.
        Assert.Equal(["k1", "k2", "k0"], new GeminiOptions { ApiKey = "k0", ApiKeys = "k1, k2\nk0" }.ResolveKeys());
        Assert.Equal(["k9", "k1"], new GeminiOptions { ApiKey = "k9", ApiKeys = "k1" }.ResolveKeys());
        Assert.Empty(new GeminiOptions().ResolveKeys());
        var pool = Pool();
        Assert.Equal([0, 1, 2], pool.GetAttemptOrder(Model));
        Assert.Equal([1, 2, 0], pool.GetAttemptOrder(Model));
        Assert.Equal([2, 0, 1], pool.GetAttemptOrder(Model));
        Assert.Empty(Pool(null, null).GetAttemptOrder(Model));
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, null, GeminiFailure.RateLimited)]
    [InlineData(HttpStatusCode.BadRequest, "{\"error\":{\"status\":\"RESOURCE_EXHAUSTED\"}}", GeminiFailure.RateLimited)]
    [InlineData(HttpStatusCode.Forbidden, null, GeminiFailure.Unauthorized)]
    [InlineData(HttpStatusCode.BadRequest, "API_KEY_INVALID", GeminiFailure.Unauthorized)]
    [InlineData(HttpStatusCode.NotFound, null, GeminiFailure.BadRequest)]
    [InlineData(HttpStatusCode.BadRequest, "INVALID_ARGUMENT", GeminiFailure.BadRequest)]
    [InlineData(HttpStatusCode.InternalServerError, null, GeminiFailure.Transient)]
    [InlineData(null, null, GeminiFailure.Transient)]
    public void Errors_are_classified(HttpStatusCode? status, string? body, GeminiFailure expected) =>
        Assert.Equal(expected, GeminiKeyPool.Classify(status, body));

    [Theory]
    [InlineData(GeminiFailure.RateLimited, 30)]
    [InlineData(GeminiFailure.Unauthorized, 360)]
    [InlineData(GeminiFailure.BadRequest, 2)]
    public void Each_failure_class_has_its_cooldown_per_key_and_model(GeminiFailure failure, int minutes)
    {
        var pool = Pool();
        pool.ReportFailure(1, Model, failure);
        Assert.Equal(_clock.Now.AddMinutes(minutes), pool.SuspendedUntil(1, Model));
        Assert.Null(pool.SuspendedUntil(1, "gemini-2.0-flash")); // other model unaffected
        Assert.Equal([0, 2, 1], pool.GetAttemptOrder(Model));    // suspended key last
        _clock.Advance(TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds(1));
        Assert.Equal([1, 2, 0], pool.GetAttemptOrder(Model));    // released again
    }

    [Fact]
    public void Transient_failures_suspend_after_three_in_a_row_and_success_resets()
    {
        var pool = Pool();
        pool.ReportFailure(0, Model, GeminiFailure.Transient);
        pool.ReportFailure(0, Model, GeminiFailure.Transient);
        pool.ReportSuccess(0, Model);
        pool.ReportFailure(0, Model, GeminiFailure.Transient);
        pool.ReportFailure(0, Model, GeminiFailure.Transient);
        Assert.Null(pool.SuspendedUntil(0, Model));
        pool.ReportFailure(0, Model, GeminiFailure.Transient);
        Assert.Equal(_clock.Now.AddMinutes(10), pool.SuspendedUntil(0, Model));
        pool.ReportSuccess(0, Model);
        Assert.Null(pool.SuspendedUntil(0, Model));
    }

    [Fact]
    public void When_every_key_is_suspended_they_are_tried_by_soonest_release()
    {
        var pool = Pool();
        pool.ReportFailure(0, Model, GeminiFailure.Unauthorized);
        pool.ReportFailure(1, Model, GeminiFailure.RateLimited);
        pool.ReportFailure(2, Model, GeminiFailure.BadRequest);
        Assert.Equal([2, 1, 0], pool.GetAttemptOrder(Model));
    }

    [Fact]
    public async Task Client_rotates_to_the_next_key_and_never_logs_keys()
    {
        var logs = new ListLogger<GeminiClient>();
        var pool = Pool("secret-a", "secret-b");
        var handler = new ScriptedHandler(request =>
            request.Headers.GetValues("x-goog-api-key").Single() == "secret-a"
                ? ScriptedHandler.Json("""{"error":{"status":"RESOURCE_EXHAUSTED"}}""", HttpStatusCode.TooManyRequests)
                : ScriptedHandler.Json("""{"candidates":[{"content":{"parts":[{"text":"```json\n{\"a\":1}\n```"}]}}]}"""));
        var client = new GeminiClient(new HttpClient(handler), pool,
            Options.Create(new GeminiOptions { ApiKey = "secret-a", ApiKeys = "secret-b" }), logs);

        Assert.Equal("{\"a\":1}", await client.GenerateJsonAsync(Model, "sys", "user", interactive: true, default));
        Assert.Equal(2, handler.Requests.Count);
        Assert.NotNull(pool.SuspendedUntil(0, Model));
        using var body = JsonDocument.Parse(handler.Bodies[0]);
        var config = body.RootElement.GetProperty("generationConfig");
        Assert.Equal(("application/json", 0), (config.GetProperty("responseMimeType").GetString(),
            config.GetProperty("thinkingConfig").GetProperty("thinkingBudget").GetInt32()));
        Assert.EndsWith("/models/gemini-2.5-flash:generateContent", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.DoesNotContain(logs.Messages, m => m.Contains("secret"));
        Assert.Contains(logs.Messages, m => m.Contains("#0"));
    }

    [Fact]
    public async Task Interactive_calls_try_at_most_two_keys_and_batch_calls_try_all()
    {
        var pool = new GeminiKeyPool(Options.Create(new GeminiOptions { ApiKeys = "a,b,c,d" }), _clock,
            NullLogger<GeminiKeyPool>.Instance);
        var handler = new ScriptedHandler(_ => ScriptedHandler.Json("{}", HttpStatusCode.ServiceUnavailable));
        var client = new GeminiClient(new HttpClient(handler), pool,
            Options.Create(new GeminiOptions { ApiKeys = "a,b,c,d" }), NullLogger<GeminiClient>.Instance);
        Assert.Null(await client.GenerateJsonAsync("gemini-2.0-flash", "s", "u", interactive: true, default));
        Assert.Equal(2, handler.Requests.Count);
        Assert.DoesNotContain("thinkingConfig", handler.Bodies[0]);
        Assert.Null(await client.GenerateJsonAsync("gemini-2.0-flash", "s", "u", interactive: false, default));
        Assert.Equal(6, handler.Requests.Count);
    }

    [Fact]
    public void Word_batch_output_is_truncated_and_normalised()
    {
        var parsed = GeminiWordProvider.ParseWordBatch($$"""
            { "Budget": { "meaningVi": "{{new string('n', 300)}}", "phonetic": "ˈbʌdʒɪt", "partOfSpeech": "n",
                          "example": "We cut the budget.", "exampleVi": "Chúng tôi cắt giảm ngân sách." },
              "broken": "x" }
            """);
        var budget = Assert.Single(parsed).Value;
        Assert.Equal((200, "/ˈbʌdʒɪt/", PartOfSpeech.Noun), (budget.MeaningVi!.Length, budget.Phonetic, budget.PartOfSpeech!.Value));
        Assert.True(parsed.ContainsKey("budget")); // case-insensitive keys
        Assert.Empty(GeminiWordProvider.ParseWordBatch("not json"));
        var phrase = GeminiWordProvider.ParsePhrase("""{ "meaningVi": "mong đợi", "type": "phrasal verb", "usage": "look forward to + V-ing" }""");
        Assert.Equal(("mong đợi", "phrasal_verb"), (phrase!.MeaningVi, phrase.PhraseType));
        Assert.Null(GeminiWordProvider.ParsePhrase("""{ "type": "poem" }"""));
        Assert.Equal("{\"x\":1}", GeminiClient.ExtractJson("Sure! {\"x\":1} done"));
        Assert.Null(GeminiClient.ExtractJson("no json"));
    }

    [Fact]
    public void Prompts_are_embedded_with_placeholders()
    {
        foreach (var name in new[] { "word-batch", "phrase-analysis", "translation" })
        {
            var prompt = PromptTemplate.Load(name);
            Assert.False(string.IsNullOrWhiteSpace(prompt.System));
            Assert.Contains("{{", prompt.User);
        }
        Assert.Contains("[\"a\"]", PromptTemplate.Load("word-batch").Render(new Dictionary<string, string> { ["words"] = "[\"a\"]" }));
    }
}

public sealed class FreeProviderTests
{
    [Fact]
    public async Task Dictionary_parses_entries_url_encodes_and_opens_the_circuit()
    {
        var clock = new FixedTimeProvider(DateTimeOffset.UtcNow);
        var breaker = new DictionaryCircuitBreaker(clock);
        var fail = false;
        var handler = new ScriptedHandler(request => fail
            ? ScriptedHandler.Json("{}", HttpStatusCode.InternalServerError)
            : request.RequestUri!.AbsolutePath.EndsWith("/unknown")
                ? ScriptedHandler.Json("{}", HttpStatusCode.NotFound)
                : ScriptedHandler.Json("""
                    [ { "phonetic": "", "phonetics": [ { "text": "" }, { "text": "/ˈrɛsəmeɪ/" } ],
                        "meanings": [ { "partOfSpeech": "noun", "definitions": [
                            { "definition": "A summary of work experience.", "example": "Send your résumé." },
                            { "definition": "A short account." }, { "definition": "Third." } ] } ] } ]
                    """));
        var provider = new FreeDictionaryProvider(new HttpClient(handler), breaker, NullLogger<FreeDictionaryProvider>.Instance);

        var gloss = await provider.LookupAsync("résumé", default);
        Assert.Equal(("/ˈrɛsəmeɪ/", PartOfSpeech.Noun, "(noun) A summary of work experience. · (noun) A short account.",
            "Send your résumé.", (string?)null), (gloss!.Phonetic, gloss.PartOfSpeech!.Value, gloss.DefinitionEn, gloss.Example, gloss.MeaningVi));
        Assert.Equal("/api/v2/entries/en/r%C3%A9sum%C3%A9", handler.Requests[0].RequestUri!.AbsolutePath);
        Assert.Null(await provider.LookupAsync("unknown", default));
        Assert.Null(await provider.LookupAsync("two words", default));

        fail = true;
        for (var i = 0; i < 3; i++) Assert.Null(await provider.LookupAsync("word", default));
        Assert.True(breaker.IsOpen);
        var calls = handler.Requests.Count;
        Assert.Null(await provider.LookupAsync("word", default));
        Assert.Equal(calls, handler.Requests.Count); // open circuit: no call
        clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False(breaker.IsOpen);
    }

    [Fact]
    public async Task Translation_falls_back_through_providers_and_rejects_junk()
    {
        var handler = new ScriptedHandler(request => request.RequestUri!.Host switch
        {
            "translation.googleapis.com" => ScriptedHandler.Json("{}", HttpStatusCode.Forbidden),
            "translate.googleapis.com" => ScriptedHandler.Json("""[[["budget","budget",null]],null,"en"]"""),
            _ => ScriptedHandler.Json("""{"responseData":{"translatedText":"ngân sách"},"responseStatus":200}""")
        });
        var provider = new TranslationProvider(new HttpClient(handler),
            Options.Create(new GoogleTranslateOptions { ApiKey = "paid-key" }), NullLogger<TranslationProvider>.Instance);
        var result = await provider.TranslateAsync("budget", default);
        Assert.Equal(("ngân sách", GlossSource.MyMemory), (result!.Text, result.Source));
        Assert.Equal(["translation.googleapis.com", "translate.googleapis.com", "api.mymemory.translated.net"],
            handler.Requests.Select(x => x.RequestUri!.Host));
        Assert.Contains("Mozilla", handler.Requests[1].Headers.UserAgent.ToString());

        var free = new TranslationProvider(new HttpClient(new ScriptedHandler(request =>
                request.RequestUri!.Host == "translate.googleapis.com"
                    ? ScriptedHandler.Json("""[[["ngân ","budget",null],["sách","x",null]]]""")
                    : ScriptedHandler.Json("{}"))),
            Options.Create(new GoogleTranslateOptions()), NullLogger<TranslationProvider>.Instance);
        Assert.Equal(("ngân sách", GlossSource.Google), ((await free.TranslateAsync("budget", default))!.Text, GlossSource.Google));

        var junk = new TranslationProvider(new HttpClient(new ScriptedHandler(request =>
                request.RequestUri!.Host == "translate.googleapis.com"
                    ? ScriptedHandler.Json("{}", HttpStatusCode.TooManyRequests)
                    : ScriptedHandler.Json("""{"responseData":{"translatedText":"MYMEMORY WARNING: YOU USED ALL AVAILABLE FREE TRANSLATIONS"}}"""))),
            Options.Create(new GoogleTranslateOptions()), NullLogger<TranslationProvider>.Instance);
        Assert.Null(await junk.TranslateAsync("budget", default));
    }
}

/// <summary>Captures formatted log messages.</summary>
public sealed class ListLogger<T> : ILogger<T>
{
    public List<string> Messages { get; } = [];
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter) => Messages.Add(formatter(state, exception));
}
