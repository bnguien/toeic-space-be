using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Application.Services.Enrichment;
using ToeicSpace.Content.Domain.Enums;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class WordEnricherTests
{
    private static readonly EnrichOptions NoAi = new(false, true, TimeSpan.FromSeconds(5));
    private static readonly EnrichOptions WithAi = new(true, true, TimeSpan.FromSeconds(5));

    private static Dictionary<string, string> SourcesOf(EnrichResult r) =>
        r.Gloss.Sources.ToDictionary(x => x.Key.Key(), x => x.Value.Key());

    [Fact]
    public async Task Dictionary_and_translation_fill_blanks_with_provenance()
    {
        var tiers = new FakeTiers();
        tiers.Dictionary.Words["invoice"] = new WordGloss
        {
            Phonetic = "/ˈɪnvɔɪs/", PartOfSpeech = PartOfSpeech.Noun, DefinitionEn = "(noun) a bill",
            Example = "Please send the invoice.", MeaningVi = "a bill (must be ignored)"
        };
        tiers.Translation.Texts["invoice"] = "hóa đơn";
        tiers.Translation.Texts["Please send the invoice."] = "Vui lòng gửi hóa đơn.";

        var result = await tiers.Enricher().EnrichAsync(new EnrichRequest("  Invoice "), NoAi, default);
        Assert.True(result.Complete);
        Assert.False(result.AiUsed);
        Assert.Equal(("Invoice", "invoice", "hóa đơn", "Vui lòng gửi hóa đơn."),
            (result.Gloss.Headword, result.Gloss.NormalizedText, result.Gloss.MeaningVi, result.Gloss.ExampleVi));
        Assert.Equal(new Dictionary<string, string>
        {
            ["phonetic"] = "dictionary", ["partOfSpeech"] = "dictionary", ["definitionEn"] = "dictionary",
            ["example"] = "dictionary", ["meaningVi"] = "google", ["exampleVi"] = "google"
        }, SourcesOf(result));
        Assert.Empty(tiers.Ai.BatchSizes);
        var cached = Assert.Single(tiers.Cache.Saved);
        Assert.Equal("hóa đơn", cached.MeaningVi);
    }

    [Fact]
    public async Task Cache_then_catalog_stop_the_pipeline_once_complete()
    {
        var tiers = new FakeTiers();
        tiers.Cache.Entries[("budget", WordKind.Word)] = new WordGloss { MeaningVi = "ngân sách", Phonetic = "/ˈbʌdʒɪt/" };
        var official = Guid.NewGuid();
        tiers.Lexicon.Words["budget"] = new CuratedMatch(official, new WordGloss
        {
            MeaningVi = "khác", PartOfSpeech = PartOfSpeech.Noun, Example = "x"
        });
        var cached = await tiers.Enricher().EnrichAsync(new EnrichRequest("budget"), WithAi, default);
        Assert.Equal(("ngân sách", official), (cached.Gloss.MeaningVi, cached.OfficialWordId));
        Assert.Equal(new Dictionary<string, string> { ["meaningVi"] = "cache", ["phonetic"] = "cache" }, SourcesOf(cached));
        Assert.Empty(tiers.Dictionary.Calls);
        Assert.Empty(tiers.Translation.Calls);
        Assert.Empty(tiers.Ai.BatchSizes);
        Assert.Empty(tiers.Cache.Saved); // nothing new to cache

        tiers.Lexicon.Words["agenda"] = new CuratedMatch(Guid.NewGuid(), new WordGloss
        {
            MeaningVi = "chương trình họp", PartOfSpeech = PartOfSpeech.Noun, Cefr = "B1"
        });
        var catalog = await tiers.Enricher().EnrichAsync(new EnrichRequest("agenda"), WithAi, default);
        Assert.True(catalog.Complete);
        Assert.Equal("catalog", SourcesOf(catalog)["meaningVi"]);
        Assert.Empty(tiers.Dictionary.Calls);
    }

    [Fact]
    public async Task Known_fields_are_never_replaced_and_carry_no_source()
    {
        var tiers = new FakeTiers();
        tiers.Dictionary.Words["deadline"] = new WordGloss { Phonetic = "/ˈdedlaɪn/", PartOfSpeech = PartOfSpeech.Noun };
        tiers.Translation.Texts["deadline"] = "hạn chót";
        var result = await tiers.Enricher().EnrichAsync(new EnrichRequest("deadline", Known: new WordGloss
        {
            MeaningVi = "thời hạn"
        }), NoAi, default);
        Assert.Equal(("thời hạn", PartOfSpeech.Noun, "/ˈdedlaɪn/"),
            (result.Gloss.MeaningVi, result.Gloss.PartOfSpeech, result.Gloss.Phonetic));
        Assert.Equal(["phonetic", "partOfSpeech"], SourcesOf(result).Keys.Order().Reverse());
        Assert.Equal([GlossField.Phonetic, GlossField.PartOfSpeech], result.Filled.Order());

        // A complete known gloss (meaning + part of speech) stops before the dictionary.
        var complete = await tiers.Enricher().EnrichAsync(new EnrichRequest("deadline", Known: new WordGloss
        {
            MeaningVi = "thời hạn", PartOfSpeech = PartOfSpeech.Verb
        }), NoAi, default);
        Assert.Equal((PartOfSpeech.Verb, (string?)null, 1), (complete.Gloss.PartOfSpeech, complete.Gloss.Phonetic,
            tiers.Dictionary.Calls.Count));
        Assert.DoesNotContain("deadline", tiers.Translation.Calls); // meaning was known
        Assert.Empty(tiers.Cache.Saved); // the user's meaning is never cached, so nothing cacheable remains
    }

    [Fact]
    public async Task Ai_fills_what_free_tiers_missed_only_when_allowed()
    {
        var tiers = new FakeTiers();
        tiers.Ai.Words["synergy"] = new WordGloss
        {
            MeaningVi = "sức mạnh tổng hợp", Phonetic = "/ˈsɪnərdʒi/", PartOfSpeech = PartOfSpeech.Noun
        };
        var without = await tiers.Enricher().EnrichAsync(new EnrichRequest("synergy"), NoAi, default);
        Assert.False(without.Complete);
        Assert.Empty(tiers.Ai.BatchSizes);

        var with = await tiers.Enricher().EnrichAsync(new EnrichRequest("synergy"), WithAi, default);
        Assert.True(with.Complete);
        Assert.True(with.AiUsed);
        Assert.Equal("ai", SourcesOf(with)["meaningVi"]);

        tiers.Ai.IsConfigured = false;
        Assert.False((await tiers.Enricher().EnrichAsync(new EnrichRequest("synergy"), WithAi, default)).AiUsed);
    }

    [Fact]
    public async Task Fill_all_blanks_runs_ai_for_a_complete_word_and_batches_by_30()
    {
        var tiers = new FakeTiers();
        tiers.Translation.Texts["memo"] = "bản ghi nhớ";
        tiers.Dictionary.Words["memo"] = new WordGloss { PartOfSpeech = PartOfSpeech.Noun };
        tiers.Ai.Words["memo"] = new WordGloss { Example = "Read the memo.", Phonetic = "/ˈmemoʊ/" };
        var filled = await tiers.Enricher().EnrichAsync(new EnrichRequest("memo"),
            WithAi with { FillAllBlanks = true }, default);
        Assert.Equal(("bản ghi nhớ", "Read the memo.", "[ai] Read the memo."),
            (filled.Gloss.MeaningVi, filled.Gloss.Example, filled.Gloss.ExampleVi));
        Assert.Equal("ai", SourcesOf(filled)["exampleVi"]);

        var many = Enumerable.Range(0, 35).Select(i => new EnrichRequest($"word{i}")).ToArray();
        var results = await tiers.Enricher().EnrichManyAsync(many, WithAi with { Interactive = false }, default);
        Assert.Equal(35, results.Count);
        Assert.Equal([1, 30, 5], tiers.Ai.BatchSizes);
    }

    [Fact]
    public async Task Phrases_skip_the_dictionary_and_always_get_the_phrase_analysis()
    {
        var tiers = new FakeTiers();
        tiers.Translation.Texts["look forward to"] = "mong đợi";
        tiers.Ai.Phrases["look forward to"] = new PhraseAnalysis("mong chờ", "phrasal_verb", "look forward to + V-ing",
            "Dùng khi mong đợi điều gì.", "Không phải 'nhìn về phía trước'.", "I look forward to meeting you.", null);
        var result = await tiers.Enricher().EnrichAsync(new EnrichRequest("look  forward to", "We look forward to it."),
            WithAi, default);
        Assert.Equal((WordKind.Phrase, "mong đợi", "phrasal_verb", "Không phải 'nhìn về phía trước'."),
            (result.Gloss.Kind, result.Gloss.MeaningVi, result.Gloss.PhraseType, result.Gloss.LiteralMeaning));
        Assert.True(result.Complete);
        Assert.Empty(tiers.Dictionary.Calls);
        Assert.Equal(["look forward to"], tiers.Ai.PhraseCalls);
        Assert.Equal(("google", "ai"), (SourcesOf(result)["meaningVi"], SourcesOf(result)["phraseType"]));
    }

    [Fact]
    public async Task Deadline_returns_what_is_known_without_throwing()
    {
        var tiers = new FakeTiers();
        tiers.Dictionary.Delay = TimeSpan.FromSeconds(10);
        tiers.Lexicon.Words["slow"] = new CuratedMatch(Guid.NewGuid(), new WordGloss { Phonetic = "/sloʊ/" });
        var watch = System.Diagnostics.Stopwatch.StartNew();
        var result = await tiers.Enricher().EnrichAsync(new EnrichRequest("slow"),
            new EnrichOptions(true, true, TimeSpan.FromMilliseconds(200)), default);
        Assert.True(watch.Elapsed < TimeSpan.FromSeconds(5));
        Assert.Equal("/sloʊ/", result.Gloss.Phonetic);
        Assert.False(result.Complete);
        Assert.Empty(tiers.Ai.BatchSizes); // stopped at the deadline

        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            tiers.Enricher().EnrichAsync(new EnrichRequest("slow"), WithAi, cancelled.Token));
    }

    [Fact]
    public async Task Rejected_input_is_returned_incomplete_without_any_tier()
    {
        var tiers = new FakeTiers();
        var result = await tiers.Enricher().EnrichAsync(new EnrichRequest("một hai ba bốn năm sáu bảy"), WithAi, default);
        Assert.Equal((WordKind.Reject, false), (result.Gloss.Kind, result.Complete));
        Assert.Empty(tiers.Translation.Calls);
    }

    [Theory]
    [InlineData("n", PartOfSpeech.Noun)]
    [InlineData("(n)", PartOfSpeech.Noun)]
    [InlineData("[adj]", PartOfSpeech.Adjective)]
    [InlineData("Adjective", PartOfSpeech.Adjective)]
    [InlineData("v.", PartOfSpeech.Verb)]
    [InlineData("adv", PartOfSpeech.Adverb)]
    [InlineData("prep", PartOfSpeech.Preposition)]
    [InlineData("conj", PartOfSpeech.Conjunction)]
    [InlineData("pron", PartOfSpeech.Pronoun)]
    [InlineData("det", PartOfSpeech.Determiner)]
    [InlineData("interj", PartOfSpeech.Interjection)]
    [InlineData("phrasal verb", PartOfSpeech.Phrase)]
    [InlineData("idiom", PartOfSpeech.Phrase)]
    [InlineData("noun phrase", PartOfSpeech.Phrase)]
    [InlineData("danh từ", PartOfSpeech.Noun)]
    [InlineData("numeral", PartOfSpeech.Other)]
    [InlineData("gibberish", PartOfSpeech.Other)]
    public void Pos_labels_are_normalised(string label, PartOfSpeech expected) =>
        Assert.Equal(expected, PosNormalizer.Normalize(label));

    [Fact]
    public void Blank_pos_is_null_and_input_is_classified()
    {
        Assert.Null(PosNormalizer.Normalize("  "));
        Assert.Equal((WordKind.Word, "Budget", "budget"), Pick(InputNormalizer.Normalize("  Budget ")));
        Assert.Equal((WordKind.Phrase, "take part in", "take part in"), Pick(InputNormalizer.Normalize("take   part in")));
        Assert.Equal(WordKind.Phrase, InputNormalizer.Normalize("a b c d e").Kind);
        Assert.Equal(WordKind.Reject, InputNormalizer.Normalize("a b c d e f").Kind);
        Assert.Equal(WordKind.Reject, InputNormalizer.Normalize("123 456").Kind);
        Assert.Equal(WordKind.Reject, InputNormalizer.Normalize("").Kind);
        Assert.Equal(100, InputNormalizer.Normalize(new string('a', 150)).Text.Length);

        static (WordKind, string, string) Pick(NormalizedInput x) => (x.Kind, x.Text, x.Key);
    }

    [Theory]
    [InlineData("hello", "xin chào", false)]
    [InlineData("hello", "", true)]
    [InlineData("hello", "  HELLO ", true)]
    [InlineData("hello", "MYMEMORY WARNING: YOU USED ALL AVAILABLE FREE TRANSLATIONS FOR TODAY", true)]
    [InlineData("hello", "QUERY LENGTH LIMIT EXCEEDED. MAX ALLOWED QUERY : 500 CHARS", true)]
    [InlineData("hello", "'EN|XX' IS AN INVALID LANGUAGE PAIR", true)]
    public void Junk_and_echo_translations_are_rejected(string input, string output, bool junk) =>
        Assert.Equal(junk, TranslationFilter.IsJunk(input, output));
}
