using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using ToeicSpace.Content.Application.DTOs.VocabularyImport;
using ToeicSpace.Content.Application.Features.ReelBackgrounds.Queries.GetReelBackgrounds;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Application.Features.VocabularyImport.Commands.ImportVocabularyStages;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStageSetProgress;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;
using ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AbandonStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.CompleteStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetStudySession;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.DeleteFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.RemoveSavedWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateSavedWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;
using ToeicSpace.Content.Application.Services;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using Xunit;

namespace ToeicSpace.Content.Tests.Vocabulary;

/// <summary>Deterministic clock for scheduling tests.</summary>
public sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;
    public override DateTimeOffset GetUtcNow() => Now;
    public void Advance(TimeSpan by) => Now += by;
}

/// <summary>
/// SQLite in-memory database (real unique indexes and transactions). Every handler gets its own
/// <see cref="ContentDbContext"/> on the shared connection, like one HTTP request scope.
/// </summary>
public sealed class VocabularyTestContext : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<ContentDbContext> _contexts = [];

    public FixedTimeProvider Clock { get; } = new(new DateTimeOffset(2026, 11, 1, 3, 0, 0, TimeSpan.Zero));
    public Guid Learner { get; } = Guid.NewGuid();

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

    private ContentDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);

    /// <summary>A fresh context, disposed with the test context.</summary>
    public ContentDbContext Db()
    {
        var db = CreateDb();
        _contexts.Add(db);
        return db;
    }

    public ImportVocabularyStagesHandler Importer()
    {
        var db = Db();
        return new(new VocabularyImportRepository(db), new UnitOfWork(db), Clock,
            NullLogger<ImportVocabularyStagesHandler>.Instance);
    }

    public GetStagesHandler Stages() => new(new VocabularyCatalogRepository(Db()));
    public GetStageSetsHandler StageSets() => new(new VocabularyCatalogRepository(Db()));
    public GetSetDetailHandler SetDetail() => new(new VocabularyCatalogRepository(Db()));
    public CreateQuizHandler Quiz()
    {
        var db = Db();
        return new(new VocabularyCatalogRepository(db), new LearnerLibraryRepository(db));
    }
    public GetReelBackgroundsHandler Reels() => new(new ReelBackgroundRepository(Db()));

    public SubmitReviewHandler Reviewer()
    {
        var db = Db();
        return new(Recorder(db), new UnitOfWork(db), Clock);
    }

    public GetLearnerSummaryHandler Summary()
    {
        var db = Db();
        return new(new LearnerProgressRepository(db), new VocabularyCatalogRepository(db),
            new LearnerLibraryRepository(db), new StudySessionRepository(db), new VocabularyPreferencesRepository(db),
            Clock);
    }

    public GetStageSetProgressHandler SetProgress()
    {
        var db = Db();
        return new(new LearnerProgressRepository(db), new VocabularyCatalogRepository(db), Clock);
    }

    public GetStudyQueueHandler Queue() => new(QueueService(Db()), Clock);

    // ---- shared application services ----
    public StudyQueueService QueueService(ContentDbContext db) => new(new LearnerProgressRepository(db),
        new VocabularyCatalogRepository(db), new LearnerLibraryRepository(db));
    public ReviewRecorder Recorder(ContentDbContext db) => new(new LearnerProgressRepository(db),
        new VocabularyCatalogRepository(db), NullLogger<ReviewRecorder>.Instance);
    public StudySessionPresenter Presenter(ContentDbContext db) => new(QueueService(db), Clock);
    public LibraryWordService LibraryWords(ContentDbContext db) =>
        new(new LearnerLibraryRepository(db), new LearnerProgressRepository(db), Clock);

    // ---- library ----
    public GetLibraryHandler Library()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), new LearnerProgressRepository(db), Clock);
    }

    public GetSavedWordsHandler SavedWords()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), new LearnerProgressRepository(db), LibraryWords(db), Clock);
    }

    public CreateFolderHandler CreateFolder()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), new UnitOfWork(db), Clock, NullLogger<CreateFolderHandler>.Instance);
    }

    public UpdateFolderHandler UpdateFolder()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), new LearnerProgressRepository(db), new UnitOfWork(db), Clock);
    }

    public DeleteFolderHandler DeleteFolder()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), new UnitOfWork(db), Clock, NullLogger<DeleteFolderHandler>.Instance);
    }

    public SaveWordHandler SaveWord()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), new VocabularyCatalogRepository(db), LibraryWords(db),
            new UnitOfWork(db), Clock, NullLogger<SaveWordHandler>.Instance);
    }

    public CreateCustomWordHandler CreateCustom()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), LibraryWords(db), new UnitOfWork(db), Clock,
            NullLogger<CreateCustomWordHandler>.Instance);
    }

    public UpdateSavedWordHandler UpdateWord()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), LibraryWords(db), new UnitOfWork(db), Clock);
    }

    public RemoveSavedWordHandler RemoveWord()
    {
        var db = Db();
        return new(new LearnerLibraryRepository(db), new LearnerProgressRepository(db), new UnitOfWork(db),
            NullLogger<RemoveSavedWordHandler>.Instance);
    }

    // ---- sessions and preferences ----
    public StartStudySessionHandler StartSession()
    {
        var db = Db();
        return new(new StudySessionRepository(db), QueueService(db), Presenter(db), new UnitOfWork(db), Clock,
            NullLogger<StartStudySessionHandler>.Instance);
    }

    public AnswerStudySessionHandler Answer()
    {
        var db = Db();
        return new(new StudySessionRepository(db), Recorder(db), Presenter(db), new UnitOfWork(db), Clock);
    }

    public CompleteStudySessionHandler CompleteSession()
    {
        var db = Db();
        return new(new StudySessionRepository(db), Presenter(db), new UnitOfWork(db), Clock,
            NullLogger<CompleteStudySessionHandler>.Instance);
    }

    public AbandonStudySessionHandler AbandonSession()
    {
        var db = Db();
        return new(new StudySessionRepository(db), new UnitOfWork(db), Clock);
    }

    public GetStudySessionHandler GetSession()
    {
        var db = Db();
        return new(new StudySessionRepository(db), Presenter(db));
    }

    public GetActiveStudySessionHandler ActiveSession()
    {
        var db = Db();
        return new(new StudySessionRepository(db), Presenter(db), Clock);
    }

    public GetPreferencesHandler Preferences() => new(new VocabularyPreferencesRepository(Db()));

    public UpdatePreferencesHandler UpdatePreferences()
    {
        var db = Db();
        return new(new VocabularyPreferencesRepository(db), new UnitOfWork(db), Clock);
    }

    public Task<VocabularyImportResultDto> ImportAsync(params string[] jsonFiles) =>
        Importer().Handle(new ImportVocabularyStagesCommand(jsonFiles
            .Select((json, i) => new VocabularyImportFile($"stage-{i}.json", "application/json", json.Length, json))
            .ToArray()), default);

    public static string RepositoryFile(string relativePath)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "ToeicSpace.slnx"))) dir = dir.Parent;
        return Path.Combine(dir?.FullName ?? throw new InvalidOperationException("Repository root not found."),
            relativePath);
    }
}

/// <summary>Builds small stage files in the data/vocabulary format.</summary>
public static class StageJson
{
    private static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);

    public sealed record W(string Word, string Pos = "noun", string? Meaning = null);

    public sealed record S(string Slug, int Order, params W[] Words);

    public static string Build(string slug, int order, params S[] sets) => JsonSerializer.Serialize(new
    {
        schemaVersion = 1,
        source = "test",
        stage = new { slug, title = $"Stage {slug}", order, description = "Test stage", toeicTarget = order * 100 },
        sets = sets.Select(s => new
        {
            slug = s.Slug, title = $"Lesson {s.Slug}", order = s.Order, topic = (string?)null,
            words = s.Words.Select(w => new
            {
                word = w.Word, phonetic = $"/{w.Word}/", partOfSpeech = w.Pos,
                meaningVi = w.Meaning ?? $"nghĩa {w.Word}", definitionEn = (string?)null,
                example = $"An example with {w.Word}.", exampleVi = (string?)null, cefr = "B1",
                synonyms = Array.Empty<string>()
            })
        })
    }, Options);
}
