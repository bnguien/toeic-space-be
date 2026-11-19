using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.Application.DTOs.DictationImport;
using ToeicSpace.Content.Application.Features.Dictation.Commands.CheckDictationAnswer;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetCourseVocab;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationExercises;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationHint;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationRoadmap;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseSkeleton;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetPackCourses;
using ToeicSpace.Content.Application.Features.DictationImport.Commands.ImportDictationPacks;
using ToeicSpace.Content.Application.Features.DictationLearner.Commands.UpdateDictationLearnerState;
using ToeicSpace.Content.Application.Features.DictationLearner.Queries.GetDictationLearnerState;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Application.Models.Dictation;
using ToeicSpace.Content.Application.Models.Speech;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Tests.Vocabulary;
using Xunit;

namespace ToeicSpace.Content.Tests.Dictation;

/// <summary>Handlers against SQLite in-memory (real unique indexes), one DbContext per call like a request scope.</summary>
public sealed class DictationHandlerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<ContentDbContext> _contexts = [];
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 7, 3, 0, 0, TimeSpan.Zero));
    private readonly Guid _learner = Guid.NewGuid();
    private readonly DictationSettings _settings = new() { ProofSecret = "test", ProofIterations = 1000, PrewarmAhead = 2 };
    private readonly RecordingPrewarmQueue _prewarm = new();

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using var db = Create();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task DisposeAsync()
    {
        foreach (var db in _contexts) await db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ContentDbContext Create() => new(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);

    private ContentDbContext Db()
    {
        var db = Create();
        _contexts.Add(db);
        return db;
    }

    private static string PackJson(string pack, int level, params (string Code, string[] Texts)[] courses) =>
        JsonSerializer.Serialize(new
        {
            schemaVersion = 1, source = "studychill", pack, roadmapLevel = level,
            courses = courses.Select((c, ci) => new
            {
                code = c.Code, order = ci + 1, level, title = $"Course {c.Code}", avgWords = 3.0, vocabCount = 1,
                items = c.Texts.Select((t, ti) => new
                {
                    sourceCode = $"{c.Code}_{ti + 1:00}", order = ti + 1, topic = "office_hr", text = t,
                    translation = $"vi {t}", ipa = "/x/", voice = "andrew", rate = 0, maxPlays = (int?)null,
                    keywords = new[] { new { word = "cat", meaning = "con mèo", partOfSpeech = "noun", ipa = "", cefr = "A1" } },
                    notes = new[] { new { id = "w0", text = "the", type = "Word", explanation = "Mạo từ" } },
                    vocab = ti == 0 ? new[] { new { word = "cats", lemma = "cat", meaning = "con mèo", partOfSpeech = "noun", ipa = "" } } : [],
                }),
            }),
        });

    private Task<DictationImportResultDto> ImportAsync(params string[] files)
    {
        var db = Db();
        return new ImportDictationPacksHandler(new DictationImportRepository(db), new UnitOfWork(db), _clock,
                NullLogger<ImportDictationPacksHandler>.Instance)
            .Handle(new ImportDictationPacksCommand(files.Select((f, i) =>
                new DictationImportFile($"pack{i}.json", f.Length, f)).ToArray()), default);
    }

    private Task<DictationCheckDto> CheckAsync(int itemId, string text, Guid? learner, bool assisted = false,
        bool dryRun = false)
    {
        var db = Db();
        return new CheckDictationAnswerHandler(new DictationCatalogRepository(db), new DictationProgressRepository(db),
                new UnitOfWork(db), _clock)
            .Handle(new CheckDictationAnswerCommand(itemId, text, "andrew", 0, assisted, dryRun, learner), default);
    }

    private Task<DictationExercisesDto> ExercisesAsync(int courseId, Guid? learner)
    {
        var db = Db();
        return new GetDictationExercisesHandler(new DictationCatalogRepository(db), new DictationProgressRepository(db))
            .Handle(new GetDictationExercisesQuery(1, courseId, learner), default);
    }

    private Task<DictationRoadmapDto> RoadmapAsync(Guid? learner)
    {
        var db = Db();
        return new GetDictationRoadmapHandler(new DictationCatalogRepository(db), new DictationProgressRepository(db))
            .Handle(new GetDictationRoadmapQuery(learner), default);
    }

    private static readonly string Level1 = PackJson("toeic-250", 1,
        ("c1", ["The cat sat.", "A dog runs fast.", "Birds sing."]), ("c2", ["Good morning."]));
    private static readonly string Level3 = PackJson("toeic-650", 3, ("c3", ["I have gone home.", "She is here."]));

    [Fact]
    public async Task Import_is_idempotent_and_deactivates_what_left_the_file()
    {
        var first = await ImportAsync(Level1, Level3);
        Assert.Equal([(2, 4, 0), (1, 2, 0)], first.Packs.Select(x => (x.CoursesCreated, x.ItemsCreated, x.Unchanged)));
        var again = Assert.Single((await ImportAsync(Level1)).Packs);
        Assert.Equal((0, 0, 0, 0, 0, 6), (again.CoursesCreated, again.CoursesUpdated, again.ItemsCreated,
            again.ItemsUpdated, again.Deactivated, again.Unchanged));

        var shorter = PackJson("toeic-250", 1, ("c1", ["The cat sat.", "A dog runs very fast."]));
        var changed = Assert.Single((await ImportAsync(shorter)).Packs);
        Assert.Equal((1, 1, 2), (changed.CoursesUpdated, changed.ItemsUpdated, changed.Deactivated));
        var db = Db();
        Assert.Equal(2, await db.DictationItems.CountAsync(x => x.IsActive && x.Course!.Pack == "toeic-250"));
        Assert.False((await db.DictationCourses.SingleAsync(x => x.Code == "c2")).IsActive);
        var item = await db.DictationItems.SingleAsync(x => x.SourceCode == "c1_01");
        Assert.Equal(("cat", "con mèo"), (item.Keywords[0].Word, item.Keywords[0].Meaning));
        Assert.Equal((DictationNoteType.Word, "Mạo từ"), (item.Notes[0].Type, item.Notes[0].Explanation));
    }

    [Fact]
    public async Task Import_rejects_a_bad_file_and_accepts_gzip_through_the_controller_reader()
    {
        var error = await Assert.ThrowsAsync<ContentRuleException>(() => ImportAsync("{\"schemaVersion\":2}"));
        Assert.Equal("invalid_import_file", error.Code);
        using var output = new MemoryStream();
        await using (var gzip = new GZipStream(output, CompressionLevel.Optimal, leaveOpen: true))
            await gzip.WriteAsync(Encoding.UTF8.GetBytes(Level1));
        Assert.True(output.Length < Level1.Length);
    }

    [Fact]
    public async Task Checking_saves_progress_and_unlocks_the_next_sentence()
    {
        await ImportAsync(Level1, Level3);
        var course = await Db().DictationCourses.SingleAsync(x => x.Code == "c1");
        var items = (await ExercisesAsync(course.Id, _learner)).Exercises;
        Assert.Equal([true, false, false], items.Select(x => x.Unlocked));

        var wrong = await CheckAsync(items[0].Id, "the cat", _learner);
        Assert.Equal((false, 66.7, "The cat sat.", true), (wrong.IsCorrect, wrong.Accuracy, wrong.CorrectText, wrong.Saved));
        var right = await CheckAsync(items[0].Id, "the cat sat", _learner);
        Assert.Equal((true, 10, true, true, true), (right.IsCorrect, right.Score, right.PassedClean, right.UnlocksNext,
            right.NextUnlocked));
        var after = (await ExercisesAsync(course.Id, _learner)).Exercises;
        Assert.Equal([true, true, false], after.Select(x => x.Unlocked));
        Assert.Equal((2, 100.0), (after[0].AttemptCount, after[0].BestAccuracy));

        // Guests are graded but nothing is saved and everything stays open.
        var guest = await CheckAsync(items[1].Id, "a dog runs fast", null);
        Assert.Equal((true, false), (guest.IsCorrect, guest.Saved));
        Assert.All((await ExercisesAsync(course.Id, null)).Exercises, x => Assert.True(x.Unlocked));
    }

    [Fact]
    public async Task A_dry_run_hides_the_answer_and_a_helped_pass_does_not_unlock_from_level_three()
    {
        await ImportAsync(Level1, Level3);
        var c3 = await Db().DictationCourses.SingleAsync(x => x.Code == "c3");
        var items = (await ExercisesAsync(c3.Id, _learner)).Exercises;
        var dry = await CheckAsync(items[0].Id, "I have", _learner, dryRun: true);
        Assert.Equal((false, string.Empty, string.Empty, false), (dry.IsCorrect, dry.CorrectText, dry.Translation, dry.Saved));
        Assert.All(dry.Words.Where(w => w.Status == DictationWordStatus.Missing), w => Assert.Equal(string.Empty, w.Text));

        var helped = await CheckAsync(items[0].Id, "I have gone home", _learner, assisted: true);
        Assert.Equal((true, false, false), (helped.IsCorrect, helped.PassedClean, helped.UnlocksNext));
        Assert.False((await ExercisesAsync(c3.Id, _learner)).Exercises[1].Unlocked);
        var clean = await CheckAsync(items[0].Id, "I have gone home", _learner);
        Assert.True(clean.UnlocksNext);
    }

    [Fact]
    public async Task Roadmap_stages_follow_the_learner_progress()
    {
        await ImportAsync(Level1);
        var guest = await RoadmapAsync(null);
        var level1 = guest.Levels[0];
        Assert.Equal((5, 2, 4, "10-250"), (guest.Levels.Count, level1.Stages.Count, level1.ItemCount, level1.Band));
        Assert.Equal([DictationStageState.Current, DictationStageState.Locked], level1.Stages.Select(x => x.State));

        var c2 = level1.Stages[1];
        await CheckAsync(c2.ItemIds[0], "good morning", _learner);
        var mine = (await RoadmapAsync(_learner)).Levels[0];
        Assert.Equal([DictationStageState.Current, DictationStageState.Done], mine.Stages.Select(x => x.State));
        Assert.Equal((1, 3, 1), (mine.StagesCompleted, mine.Stages[1].Stars, mine.PassedCount));

        var db = Db();
        var pack = await new GetPackCoursesHandler(new DictationCatalogRepository(db), new DictationProgressRepository(db))
            .Handle(new GetPackCoursesQuery("toeic-250", _learner), default);
        Assert.Equal([0, 1], pack.Courses.Select(x => x.PassedCount));
    }

    [Fact]
    public async Task Skeleton_hint_and_vocab_never_reveal_more_than_one_word()
    {
        await ImportAsync(Level1);
        var db = Db();
        var item = await db.DictationItems.AsNoTracking().FirstAsync(x => x.SourceCode == "c1_01");
        var catalog = new DictationCatalogRepository(db);
        var skeleton = await new GetExerciseSkeletonHandler(catalog, new DictationProofService(_settings), _prewarm, _settings)
            .Handle(new GetExerciseSkeletonQuery(item.Id, "sonia", null), default);
        Assert.Equal([3, 3, 3], skeleton.Words.Select(w => w.Length));
        Assert.Equal(".", skeleton.Words[^1].After);
        Assert.Equal(3, skeleton.Proof.Digests.Count);
        Assert.Empty(skeleton.Keywords); // level 1 has no keyword chips
        Assert.Equal(["A dog runs fast.", "Birds sing."], _prewarm.Jobs.Select(x => x.Text));
        Assert.All(_prewarm.Jobs, x => Assert.Equal("sonia", x.VoiceKey));

        var hint = await new GetDictationHintHandler(catalog)
            .Handle(new GetDictationHintQuery(item.Id, "the", DictationHintMode.RevealWord), default);
        Assert.Equal((1, "cat"), (hint.Index, hint.Word));

        var vocab = await new GetCourseVocabHandler(catalog).Handle(new GetCourseVocabQuery(item.CourseId), default);
        Assert.Equal(("cats", "cat", 1), (vocab.Words[0].Word, vocab.Words[0].Lemma, vocab.Words[0].Sentence));
    }

    [Fact]
    public async Task Learner_state_merges_like_studychill_learner_sync()
    {
        DictationLearnerStateDto Put(UpdateDictationLearnerStateCommand command)
        {
            var db = Db();
            return new UpdateDictationLearnerStateHandler(new DictationProgressRepository(db), new UnitOfWork(db), _clock)
                .Handle(command with { LearnerId = _learner }, default).GetAwaiter().GetResult();
        }

        using var settings = JsonDocument.Parse("{\"pace\":\"chill\",\"liveCheck\":\"word\"}");
        Put(new UpdateDictationLearnerStateCommand(new() { ["c1"] = 2 }, new() { ["2026-11-07"] = 60 },
            new DictationSettingsPairDto(settings.RootElement.Clone(), null), 2000, "sonia", 2000,
            new SoundPreferencesDto(false, 0.4, "drum", "game"), 2000));
        var stale = Put(new UpdateDictationLearnerStateCommand(new() { ["c1"] = 1, ["c2"] = 3 },
            new() { ["2026-11-07"] = 30 }, null, null, "ryan", 1000));
        Assert.Equal(new Dictionary<string, int> { ["c1"] = 2, ["c2"] = 3 }, stale.Stars);
        Assert.Equal(60, stale.DailySeconds["2026-11-07"]);
        Assert.Equal("sonia", stale.Voice);

        var db = Db();
        var read = await new GetDictationLearnerStateHandler(new DictationProgressRepository(db))
            .Handle(new GetDictationLearnerStateQuery(_learner), default);
        Assert.Equal("chill", read.Settings.Basic!.Value.GetProperty("pace").GetString());
        Assert.Null(read.Settings.Pro);
        Assert.Equal((false, "drum"), (read.Sound!.Enabled, read.Sound.Tick));
        var empty = await new GetDictationLearnerStateHandler(new DictationProgressRepository(db))
            .Handle(new GetDictationLearnerStateQuery(Guid.NewGuid()), default);
        Assert.Equal((0, 0L), (empty.Stars.Count, empty.SettingsUpdatedAt));
    }

    private sealed class RecordingPrewarmQueue : ISpeechPrewarmQueue
    {
        public List<SpeechJob> Jobs { get; } = [];
        public void Enqueue(IEnumerable<SpeechJob> jobs) => Jobs.AddRange(jobs);
    }
}
