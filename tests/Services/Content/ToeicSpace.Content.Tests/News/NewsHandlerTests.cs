using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Features.News.Commands.CheckNewsDictation;
using ToeicSpace.Content.Application.Features.News.Commands.CompleteNewsArticle;
using ToeicSpace.Content.Application.Features.News.Commands.MergeGuestReadings;
using ToeicSpace.Content.Application.Features.News.Commands.OpenNewsArticle;
using ToeicSpace.Content.Application.Features.News.Commands.UpdateNewsReadingState;
using ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticle;
using ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticles;
using ToeicSpace.Content.Application.Features.NewsImport.Commands.ImportNewsArticles;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Models.News;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Tests.Vocabulary;
using Xunit;

namespace ToeicSpace.Content.Tests.News;

/// <summary>In-memory cover store.</summary>
public sealed class MemoryCoverStore : INewsCoverStore
{
    public Dictionary<string, byte[]> Files { get; } = [];
    public bool Exists(string fileName) => Files.ContainsKey(fileName);
    public Task<bool> SaveAsync(string fileName, byte[] content, CancellationToken ct)
    {
        if (Files.TryGetValue(fileName, out var current) && current.SequenceEqual(content)) return Task.FromResult(false);
        Files[fileName] = content;
        return Task.FromResult(true);
    }
    public string? GetPhysicalPath(string fileName) => null;
}

/// <summary>News handlers on SQLite in memory, fed with the committed data/news/articles.json.</summary>
public sealed class NewsHandlerTests : IAsyncLifetime
{
    private static readonly byte[] ArticlesFile =
        File.ReadAllBytes(VocabularyTestContext.RepositoryFile("data/news/articles.json"));

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<ContentDbContext> _contexts = [];
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 7, 3, 0, 0, TimeSpan.Zero));
    private readonly MemoryCoverStore _covers = new();
    private readonly NewsSettings _settings = new();
    private readonly Guid _learner = Guid.NewGuid();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var db in _contexts) await db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ContentDbContext CreateDb() => new(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);

    private ContentDbContext Db()
    {
        var db = CreateDb();
        _contexts.Add(db);
        return db;
    }

    private Task<NewsImportResultDto> ImportAsync(params NewsImportFile[] files)
    {
        var db = Db();
        return new ImportNewsArticlesHandler(new NewsArticleRepository(db), _covers, new UnitOfWork(db), _clock,
            NullLogger<ImportNewsArticlesHandler>.Instance).Handle(new ImportNewsArticlesCommand(files), default);
    }

    private static NewsImportFile Json(byte[] content, string name = "articles.json") => new(name, "application/json", content.Length, content);

    private NewsReadingService Service(ContentDbContext db) =>
        new(new NewsArticleRepository(db), new NewsReadingRepository(db));

    private Task<NewsArticleListDto> ListAsync(GetNewsArticlesQuery query)
    {
        var db = Db();
        return new GetNewsArticlesHandler(new NewsArticleRepository(db), new NewsReadingRepository(db), _settings)
            .Handle(query, default);
    }

    private Task<NewsReadingStateDto> OpenAsync(string slug)
    {
        var db = Db();
        return new OpenNewsArticleHandler(Service(db), new NewsReadingRepository(db), new UnitOfWork(db), _clock)
            .Handle(new OpenNewsArticleCommand(slug, _learner), default);
    }

    private Task<NewsReadingStateDto> UpdateAsync(UpdateNewsReadingStateCommand command)
    {
        var db = Db();
        return new UpdateNewsReadingStateHandler(Service(db), new UnitOfWork(db), _clock)
            .Handle(command with { LearnerId = _learner }, default);
    }

    private Task<DictationResultDto> DictationAsync(string slug, string sentenceId, string text, Guid learner)
    {
        var db = Db();
        return new CheckNewsDictationHandler(Service(db), new UnitOfWork(db), _clock)
            .Handle(new CheckNewsDictationCommand(slug, sentenceId, text, false, learner), default);
    }

    [Fact]
    public async Task Imports_the_committed_articles_and_reimport_is_unchanged()
    {
        var expected = JsonNode.Parse(ArticlesFile)!["articles"]!.AsArray().Count;
        var first = await ImportAsync(Json(ArticlesFile));
        Assert.Equal((expected, 0, 0), (first.ArticlesCreated, first.ArticlesUpdated, first.ArticlesUnchanged));
        Assert.Equal(expected, first.MissingCovers.Count); // covers not uploaded in this call

        var again = await ImportAsync(Json(ArticlesFile));
        Assert.Equal((0, 0, expected), (again.ArticlesCreated, again.ArticlesUpdated, again.ArticlesUnchanged));
        Assert.Equal(expected, await Db().NewsArticles.CountAsync());
    }

    [Fact]
    public async Task Gzip_documents_and_covers_are_accepted_and_bad_files_rejected()
    {
        using var packed = new MemoryStream();
        await using (var gzip = new System.IO.Compression.GZipStream(packed, System.IO.Compression.CompressionLevel.Fastest, true))
            await gzip.WriteAsync(ArticlesFile);
        var name = (string)JsonNode.Parse(ArticlesFile)!["articles"]![0]!["cover"]!["file"]!;
        var webp = Encoding.ASCII.GetBytes("RIFF\0\0\0\0WEBPVP8 data");
        var result = await ImportAsync(Json(packed.ToArray(), "articles.json.gz"), new NewsImportFile(name, "image/webp", webp.Length, webp));
        Assert.Equal(1, result.CoversStored);
        Assert.DoesNotContain(result.MissingCovers, slug => slug == (string)JsonNode.Parse(ArticlesFile)!["articles"]![0]!["slug"]!);

        var notWebp = Encoding.ASCII.GetBytes("<svg/>");
        var error = await Assert.ThrowsAsync<ContentRuleException>(() =>
            ImportAsync(new NewsImportFile("0123456789abcdef.webp", "image/webp", notWebp.Length, notWebp)));
        Assert.Equal("invalid_import_file", error.Code);
        await Assert.ThrowsAsync<ContentRuleException>(() => ImportAsync(Json("{\"schemaVersion\":2,\"articles\":[]}"u8.ToArray())));
    }

    [Fact]
    public async Task Lists_filter_on_the_server_and_report_facets()
    {
        await ImportAsync(Json(ArticlesFile));
        var all = await ListAsync(new GetNewsArticlesQuery(PageSize: 12));
        Assert.Equal(287, all.Total);
        Assert.Equal(12, all.Items.Count);
        Assert.Equal(287, all.Facets.Stage.Values.Sum());
        Assert.Equal(6, all.Facets.Topic.Count);
        Assert.All(all.Items, x => Assert.StartsWith("/content/api/v1/news/covers/", x.CoverUrl));
        Assert.Equal((0, 0), (all.DoneCount, all.ReadingCount));

        var c1 = await ListAsync(new GetNewsArticlesQuery(Stage: "c1"));
        Assert.Equal(all.Facets.Stage["c1"], c1.Total);
        Assert.All(c1.Items, x => Assert.Equal("c1", x.Stage));
        var shortOnes = await ListAsync(new GetNewsArticlesQuery(Length: "short", Topic: "education"));
        Assert.All(shortOnes.Items, x => Assert.True(x.Minutes < 3 && x.Topic == "education"));
        var unknown = await ListAsync(new GetNewsArticlesQuery(Stage: "all", PageSize: 1));
        Assert.Equal(287, unknown.Total);
        var guestReading = await ListAsync(new GetNewsArticlesQuery(State: "reading"));
        Assert.Equal(0, guestReading.Total);

        // Pages do not overlap.
        var page2 = await ListAsync(new GetNewsArticlesQuery(Page: 2, PageSize: 12));
        Assert.Empty(all.Items.Select(x => x.Slug).Intersect(page2.Items.Select(x => x.Slug)));
    }

    [Fact]
    public async Task Learner_state_is_saved_and_reflected_in_the_list()
    {
        await ImportAsync(Json(ArticlesFile));
        var detail = await new GetNewsArticleHandler(new NewsArticleRepository(Db()), _settings)
            .Handle(new GetNewsArticleQuery("voa-2453053"), default);
        var opened = await OpenAsync(detail.Slug);
        Assert.False(opened.Done);
        Assert.Null(opened.Settings);
        Assert.Equal(detail.Paragraphs.Sum(p => p.Sentences.Count), opened.DictationTotal);

        var vocabularyWord = detail.Vocabulary[0].Word;
        var state = await UpdateAsync(new UpdateNewsReadingStateCommand(detail.Slug, ReadingSeconds: 95,
            KnownWords: [vocabularyWord, "not-in-article"], Notes: "ghi chú",
            Settings: new NewsReaderSettingsDto("ink", "bilingual", "columns", 20, true, 360, 55, true, false, true, "dictation", null)));
        Assert.Equal((95, "ghi chú"), (state.ReadingSeconds, state.Notes));
        Assert.Equal([vocabularyWord], state.KnownWords);
        Assert.Equal(20, state.Settings!.FontSize);

        state = await UpdateAsync(new UpdateNewsReadingStateCommand(detail.Slug, ReadingSeconds: 10));
        Assert.Equal(95, state.ReadingSeconds);

        // Settings follow the learner to another article.
        var other = await OpenAsync("voa-3947928");
        Assert.Equal(("columns", 20), (other.Settings!.Layout, other.Settings.FontSize));

        // Dictation: guests are graded only, a learner's attempt is recorded.
        var sentence = detail.Paragraphs[0].Sentences[0];
        var guest = await DictationAsync(detail.Slug, sentence.Id, sentence.Text, Guid.Empty);
        Assert.Equal((true, false), (guest.Correct, guest.Recorded));
        var mine = await DictationAsync(detail.Slug, sentence.Id, sentence.Text.ToUpperInvariant(), _learner);
        Assert.Equal((true, true), (mine.Correct, mine.Recorded));
        await Assert.ThrowsAsync<ContentRuleException>(() => DictationAsync(detail.Slug, "p99s9", "x", _learner));

        var db = Db();
        var completed = await new CompleteNewsArticleHandler(Service(db), new UnitOfWork(db), _clock)
            .Handle(new CompleteNewsArticleCommand(detail.Slug, _learner), default);
        Assert.True(completed.Done);
        Assert.True(completed.Dictation[sentence.Id].Passed && completed.Dictation[sentence.Id].Unaided);
        Assert.True(completed.Dictation[sentence.Id].Result!.Correct);

        var list = await ListAsync(new GetNewsArticlesQuery(PageSize: 60, LearnerId: _learner));
        Assert.Equal((1, 1), (list.DoneCount, list.ReadingCount));
        var reading = await ListAsync(new GetNewsArticlesQuery(State: "reading", LearnerId: _learner));
        Assert.Equal(["voa-3947928"], reading.Items.Select(x => x.Slug));
        var done = await ListAsync(new GetNewsArticlesQuery(State: "done", LearnerId: _learner));
        Assert.Equal(["voa-2453053"], done.Items.Select(x => x.Slug));
        Assert.True(done.Items[0].Done);
        Assert.Equal(95, done.Items[0].ReadingSeconds);
    }

    [Fact]
    public async Task Guest_state_merges_with_larger_clock_and_newer_notes()
    {
        await ImportAsync(Json(ArticlesFile));
        await OpenAsync("voa-2453053");
        await UpdateAsync(new UpdateNewsReadingStateCommand("voa-2453053", ReadingSeconds: 200, Notes: "server"));

        var db = Db();
        var result = await new MergeGuestReadingsHandler(new NewsArticleRepository(db), new NewsReadingRepository(db),
                new UnitOfWork(db), _clock)
            .Handle(new MergeGuestReadingsCommand(
            [
                new NewsGuestReadingInput("voa-2453053", 50, false, null, "old guest note", null, _clock.GetUtcNow().AddDays(-1)),
                new NewsGuestReadingInput("voa-3947928", 40, true, null, "guest", new Dictionary<string, NewsDictationEntryInput>
                {
                    ["p1s1"] = new(true, true, false, 1, "x"), ["p99s1"] = new(true, true, false, 1, "x")
                }),
                new NewsGuestReadingInput("missing", 5)
            ], _learner), default);
        Assert.Equal((2, 1), (result.Merged, result.Skipped));

        var rows = await Db().NewsReadings.AsNoTracking().Include(x => x.Article).ToListAsync();
        var kept = rows.Single(x => x.Article!.Slug == "voa-2453053");
        Assert.Equal((200, "server"), (kept.ReadingSeconds, kept.Notes));
        var merged = rows.Single(x => x.Article!.Slug == "voa-3947928");
        Assert.Equal((40, "guest", true), (merged.ReadingSeconds, merged.Notes, merged.IsDone));
        Assert.Equal(["p1s1"], merged.Dictation.Keys);
    }
}
