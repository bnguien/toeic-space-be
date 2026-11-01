using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.Application.DTOs.LibraryImport;
using ToeicSpace.Content.Application.Features.LibraryImport.Commands.ImportWords;
using ToeicSpace.Content.Application.Features.LibraryImport.Commands.ParseImport;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.AiFillWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLookup.Queries.LookupWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;
using ToeicSpace.Content.Application.Models.Enrichment;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Infrastructure.Services.Enrichment;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

public sealed class LibraryImportTests : IAsyncLifetime
{
    private readonly VocabularyTestContext _ctx = new();
    private readonly FakeTiers _tiers = new();
    private readonly EnrichmentSettings _settings = new();
    private Dictionary<string, Guid> _official = [];
    private Guid Learner => _ctx.Learner;

    public async Task InitializeAsync()
    {
        await _ctx.InitializeAsync();
        await _ctx.ImportAsync(StageJson.Build("stage-a", 1, new StageJson.S("lesson-1", 1,
            new("budget", Meaning: "ngân sách"), new("report", "verb", "báo cáo"), new("invoice", Meaning: "hóa đơn"))));
        _official = await _ctx.Db().VocabularyWords.ToDictionaryAsync(x => x.Headword, x => x.Id);
        _tiers.Ai.IsConfigured = false;
    }

    public Task DisposeAsync() => _ctx.DisposeAsync();

    private ImportWordsHandler Importer()
    {
        var db = _ctx.Db();
        return new ImportWordsHandler(new LearnerLibraryRepository(db), new LearnerProgressRepository(db),
            new CuratedLexicon(db), _tiers.Enricher(new CuratedLexicon(db)), _ctx.LibraryWords(db), _settings,
            new UnitOfWork(db), _ctx.Clock, NullLogger<ImportWordsHandler>.Instance);
    }

    private Task<ImportResultDto> Import(IReadOnlyList<ImportItemDto> items, Guid? folder = null, bool categories = false) =>
        Importer().Handle(new ImportWordsCommand(items, folder, categories, 0, Learner), default);

    private static async Task<string> Code(Func<Task> action) =>
        (await Assert.ThrowsAsync<ContentRuleException>(action)).Code;

    [Fact]
    public async Task Official_matches_are_saved_as_official_words_and_others_become_custom_words()
    {
        _tiers.Translation.Texts["stapler"] = "cái dập ghim";
        _tiers.Dictionary.Words["stapler"] = new WordGloss { Phonetic = "/ˈsteɪplər/", PartOfSpeech = PartOfSpeech.Noun };
        var result = await Import([
            new("Budget"),                                         // official, any POS
            new("report", PartOfSpeech: PartOfSpeech.Noun, MeaningVi: "bản báo cáo"), // POS differs → custom
            new("stapler"),                                        // enriched custom
            new("123"),                                            // rejected
            new("mystery word")                                    // no meaning anywhere
        ]);
        Assert.Equal((3, 0), (result.Created, result.Updated));
        Assert.Equal([(3, "no_latin_letter"), (4, "meaning_missing")], result.Failed.Select(x => (x.Index, x.Code)));

        var db = _ctx.Db();
        Assert.Equal(1, await db.SavedWords.CountAsync(x => x.WordId == _official["budget"]));
        var custom = await db.VocabularyWords.Where(x => x.OwnerKey == Learner).OrderBy(x => x.Headword).ToListAsync();
        Assert.Equal([("report", PartOfSpeech.Noun, "bản báo cáo"), ("stapler", PartOfSpeech.Noun, "cái dập ghim")],
            custom.Select(x => (x.Headword, x.PartOfSpeech, x.MeaningVi)));
        Assert.Equal("/ˈsteɪplər/", custom[1].Phonetic);
        Assert.Equal(["budget", "report", "stapler"], result.Items.Select(x => x.Word.Headword).Order());
        Assert.Contains(result.Items, x => x.Word.Headword == "budget" && !x.IsCustom);
    }

    [Fact]
    public async Task Existing_entries_are_updated_by_filling_blanks_only()
    {
        var custom = await _ctx.CreateCustom().Handle(new CreateCustomWordCommand("kickoff", null, PartOfSpeech.Noun,
            "khai mạc", null, null, null, null, null, 0, Learner), default);
        await _ctx.SaveWord().Handle(new SaveWordCommand(_official["invoice"], default, default, 0, Learner), default);
        var result = await Import([
            new("kickoff", MeaningVi: "buổi bắt đầu", Example: "Kickoff meeting today."),
            new("invoice"),
            new("Kickoff")
        ]);
        Assert.Equal((0, 3), (result.Created, result.Updated));
        // The stored meaning is kept; only the blank example is filled (the word is already complete, so no tier runs).
        var word = await _ctx.Db().VocabularyWords.SingleAsync(x => x.Id == custom.Word.Id);
        Assert.Equal(("khai mạc", "", "Kickoff meeting today."), (word.MeaningVi, word.Phonetic, word.Example));
        Assert.Empty(_tiers.Dictionary.Calls);
        Assert.Equal(2, await _ctx.Db().SavedWords.CountAsync());
    }

    [Fact]
    public async Task Categories_become_folders_reused_case_insensitively_and_duplicates_in_a_batch_merge()
    {
        var work = await _ctx.CreateFolder().Handle(new CreateFolderCommand("Work", Learner), default);
        var target = await _ctx.CreateFolder().Handle(new CreateFolderCommand("Inbox", Learner), default);
        var result = await Import([
            new("budget", Category: "WORK"),
            new("stapler", MeaningVi: "cái dập ghim", Category: "Office supplies"),
            new("STAPLER", MeaningVi: "dập ghim", Category: "office SUPPLIES"),
            new("invoice")
        ], target.Id, categories: true);
        Assert.Equal((3, 1), (result.Created, result.Updated));

        var db = _ctx.Db();
        var folders = await db.VocabularyFolders.Where(x => x.LearnerId == Learner).OrderBy(x => x.Order).ToListAsync();
        Assert.Equal(["Work", "Inbox", "Office supplies"], folders.Select(x => x.Name));
        var saved = await db.SavedWords.Include(x => x.Word).ToDictionaryAsync(x => x.Word!.NormalizedHeadword, x => x.FolderId);
        Assert.Equal((work.Id, folders[2].Id, target.Id), (saved["budget"], saved["stapler"], saved["invoice"]));
        Assert.Equal(1, await db.VocabularyWords.CountAsync(x => x.OwnerKey == Learner));

        Assert.Equal("vocabulary_folder_not_found", await Code(() => Import([new("budget")], Guid.NewGuid())));
    }

    [Fact]
    public async Task Parse_preview_marks_duplicates_and_words_already_in_the_library()
    {
        await _ctx.SaveWord().Handle(new SaveWordCommand(_official["budget"], default, default, 0, Learner), default);
        var handler = new ParseImportHandler(new LearnerLibraryRepository(_ctx.Db()));
        var preview = await handler.Handle(new ParseImportCommand("1. budget - ngân sách\n2. memo\n3. Memo\n4. ???", null, Learner), default);
        Assert.Equal([("budget", false, true), ("memo", false, false), ("Memo", true, false)],
            preview.Items.Select(x => (x.Headword, x.Duplicate, x.Existing)));
        Assert.Equal((4, "no_latin_letter"), (Assert.Single(preview.Rejected).Line, preview.Rejected[0].Code));
        var json = await handler.Handle(new ParseImportCommand(null, """{ "budget": "ngân sách" }""", Learner), default);
        Assert.True(Assert.Single(json.Items).Existing);
    }

    [Fact]
    public async Task Ai_fill_only_fills_blank_fields_of_custom_words()
    {
        var custom = await _ctx.CreateCustom().Handle(new CreateCustomWordCommand("onboarding", null, PartOfSpeech.Noun,
            "hội nhập nhân viên mới", null, "Onboarding starts Monday.", null, null, null, 0, Learner), default);
        _tiers.Ai.IsConfigured = true;
        _tiers.Ai.Words["onboarding"] = new WordGloss
        {
            MeaningVi = "khác", Phonetic = "ˈɒnbɔːdɪŋ", Example = "khác", ExampleVi = "Hội nhập bắt đầu thứ Hai."
        };
        AiFillWordHandler Handler()
        {
            var db = _ctx.Db();
            return new AiFillWordHandler(new LearnerLibraryRepository(db), _tiers.Enricher(new CuratedLexicon(db)),
                _ctx.LibraryWords(db), _settings, new UnitOfWork(db), _ctx.Clock, NullLogger<AiFillWordHandler>.Instance);
        }
        var result = await Handler().Handle(new AiFillWordCommand(custom.Id, Learner), default);
        Assert.Equal(["phonetic", "exampleVi"], result.Filled);
        Assert.Equal(("hội nhập nhân viên mới", "ˈɒnbɔːdɪŋ", "Onboarding starts Monday.", "Hội nhập bắt đầu thứ Hai."),
            (result.Word.MeaningVi, result.Word.Phonetic, result.Word.Example, result.Word.ExampleVi));
        Assert.Equal(custom.Id, result.Id);
        Assert.Empty((await Handler().Handle(new AiFillWordCommand(custom.Id, Learner), default)).Filled);

        var official = await _ctx.SaveWord().Handle(new SaveWordCommand(_official["budget"], default, default, 0, Learner), default);
        Assert.Equal("word_not_custom", await Code(() => Handler().Handle(new AiFillWordCommand(official.SavedWord.Id, Learner), default)));
        Assert.Equal("saved_word_not_found", await Code(() => Handler().Handle(new AiFillWordCommand(custom.Id, Guid.NewGuid()), default)));
    }

    [Fact]
    public async Task Lookup_reports_the_official_word_and_the_existing_library_entry()
    {
        LookupWordHandler Handler()
        {
            var db = _ctx.Db();
            return new LookupWordHandler(_tiers.Enricher(new CuratedLexicon(db)), new LearnerLibraryRepository(db), _settings);
        }
        var first = await Handler().Handle(new LookupWordQuery("Budget", null, false, Learner), default);
        Assert.Equal(("word", _official["budget"], "ngân sách", "catalog", (Application.DTOs.VocabularyLookup.ExistingWordDto?)null),
            (first.Kind, first.OfficialWordId, first.MeaningVi, first.Sources["meaningVi"], first.Existing));
        Assert.True(first.Complete);

        var saved = await _ctx.SaveWord().Handle(new SaveWordCommand(_official["budget"], default, default, 0, Learner), default);
        Assert.Equal(saved.SavedWord.Id, (await Handler().Handle(new LookupWordQuery("budget", null, false, Learner), default)).Existing!.SavedWordId);

        var custom = await _ctx.CreateCustom().Handle(new CreateCustomWordCommand("synergy", null, PartOfSpeech.Noun,
            "cộng hưởng", null, null, null, null, null, 0, Learner), default);
        var mine = await Handler().Handle(new LookupWordQuery("synergy", null, false, Learner), default);
        Assert.Equal((custom.Id, (Guid?)null), (mine.Existing!.SavedWordId, mine.OfficialWordId));

        Assert.False(new LookupWordValidator().Validate(new LookupWordQuery("a b c d e f", null, false, Learner)).IsValid);
        Assert.False(new LookupWordValidator().Validate(new LookupWordQuery("???", null, false, Learner)).IsValid);
        Assert.True(new LookupWordValidator().Validate(new LookupWordQuery("take part in", "ctx", true, Learner)).IsValid);
    }

    [Fact]
    public void Import_and_parse_requests_are_validated()
    {
        var import = new ImportWordsValidator();
        Assert.True(import.Validate(new ImportWordsCommand([new("budget")], LearnerId: Learner)).IsValid);
        Assert.False(import.Validate(new ImportWordsCommand([], LearnerId: Learner)).IsValid);
        Assert.False(import.Validate(new ImportWordsCommand(Enumerable.Repeat(new ImportItemDto("a"), 51).ToArray(), LearnerId: Learner)).IsValid);
        Assert.False(import.Validate(new ImportWordsCommand([new("budget", MeaningVi: new string('x', 201))], LearnerId: Learner)).IsValid);
        Assert.False(import.Validate(new ImportWordsCommand([new("")], LearnerId: Learner)).IsValid);
        Assert.False(import.Validate(new ImportWordsCommand([new("a", Category: new string('c', 61))], LearnerId: Learner)).IsValid);
        Assert.False(import.Validate(new ImportWordsCommand([new("a")], Guid.Empty, LearnerId: Learner)).IsValid);

        var parse = new ParseImportValidator();
        Assert.True(parse.Validate(new ParseImportCommand("budget", null, Learner)).IsValid);
        Assert.False(parse.Validate(new ParseImportCommand("budget", "[]", Learner)).IsValid);
        Assert.False(parse.Validate(new ParseImportCommand(null, null, Learner)).IsValid);
        Assert.False(parse.Validate(new ParseImportCommand(new string('a', 20_001), null, Learner)).IsValid);
    }
}

public sealed class WordGlossCacheTests : IAsyncLifetime
{
    private readonly Microsoft.Data.Sqlite.SqliteConnection _connection = new("DataSource=:memory:");
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero));
    private ServiceProvider _services = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        _services = new ServiceCollection().AddDbContext<ContentDbContext>(o => o.UseSqlite(_connection)).BuildServiceProvider();
        await using var scope = _services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<ContentDbContext>().Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        await _services.DisposeAsync();
        await _connection.DisposeAsync();
    }

    [Fact]
    public async Task Cache_stores_meaningful_glosses_with_sources_and_expires_after_the_ttl()
    {
        var cache = new WordGlossCacheRepository(_services.GetRequiredService<IServiceScopeFactory>(),
            new EnrichmentSettings { CacheTtlDays = 30 }, _clock, NullLogger<WordGlossCacheRepository>.Instance);
        var gloss = new WordGloss { Text = "Budget", NormalizedText = "budget", Headword = "Budget", Kind = WordKind.Word };
        await cache.SetAsync(gloss, default);
        Assert.Null(await cache.GetAsync("budget", WordKind.Word, default)); // no meaning, not cached

        gloss.FillText(GlossField.MeaningVi, "ngân sách", GlossSource.Google);
        gloss.FillPartOfSpeech(PartOfSpeech.Noun, GlossSource.Dictionary);
        await cache.SetAsync(gloss, default);
        gloss.FillText(GlossField.Phonetic, "/ˈbʌdʒɪt/", GlossSource.Ai);
        await cache.SetAsync(gloss, default); // upsert, no duplicate row

        var hit = await cache.GetAsync("budget", WordKind.Word, default);
        Assert.Equal(("ngân sách", PartOfSpeech.Noun, "/ˈbʌdʒɪt/"), (hit!.MeaningVi, hit.PartOfSpeech!.Value, hit.Phonetic));
        Assert.Equal(GlossSource.Google, hit.Sources[GlossField.MeaningVi]);
        Assert.Null(await cache.GetAsync("budget", WordKind.Phrase, default));
        await using (var scope = _services.CreateAsyncScope())
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ContentDbContext>().WordGlossCache.CountAsync());

        _clock.Advance(TimeSpan.FromDays(31));
        Assert.Null(await cache.GetAsync("budget", WordKind.Word, default));
    }
}
