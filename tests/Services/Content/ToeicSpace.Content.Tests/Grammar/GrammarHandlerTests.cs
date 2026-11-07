using System.Text.Json.Nodes;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Features.GrammarAttempts;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.AnswerQuestion;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RetryAttemptAnswer;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitStageAttempt;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitTopicAttempt;
using ToeicSpace.Content.Application.Features.GrammarImport.Commands.ImportGrammarContent;
using ToeicSpace.Content.Application.Features.GrammarProgress.Commands.CompleteTopicPart;
using ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarMistakes;
using ToeicSpace.Content.Application.Features.GrammarProgress.Queries.GetGrammarProgress;
using ToeicSpace.Content.Application.Services.Grammar;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;
using ToeicSpace.Content.Infrastructure.Persistence;
using ToeicSpace.Content.Infrastructure.Persistence.Repositories;
using ToeicSpace.Content.Infrastructure.Services.Grammar;
using ToeicSpace.Content.Tests.Vocabulary;
using Xunit;

namespace ToeicSpace.Content.Tests.Grammar;

/// <summary>Grammar handlers on an SQLite in-memory database with the real imported content.</summary>
public sealed class GrammarHandlerTests : IAsyncLifetime
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly List<ContentDbContext> _contexts = [];
    private readonly GrammarContentCache _cache = new();
    private readonly FixedTimeProvider _clock = new(new DateTimeOffset(2026, 11, 7, 3, 0, 0, TimeSpan.Zero));
    private readonly Guid _learner = Guid.NewGuid();
    private HmacGuestAttemptTokens _tokens = null!;
    private JsonObject _data = null!;

    public async Task InitializeAsync()
    {
        await _connection.OpenAsync();
        await using (var db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options))
            await db.Database.EnsureCreatedAsync();
        _tokens = new HmacGuestAttemptTokens(Options.Create(new GrammarOptions()), _clock, NullLogger<HmacGuestAttemptTokens>.Instance);
        _data = GrammarTestData.Bundle();
        var db2 = Db();
        var result = await new ImportGrammarContentHandler(new GrammarContentRepository(db2), new UnitOfWork(db2), _cache, _clock,
                NullLogger<ImportGrammarContentHandler>.Instance)
            .Handle(new ImportGrammarContentCommand([new GrammarImportFile("grammar-content.json.gz", null, GrammarTestData.Gzip.Length,
                GrammarTestData.Gzip)]), default);
        Assert.Equal((44, 5, 137, 1842), (result.Topics, result.StageTests, result.DocumentsCreated, result.Questions));
    }

    public async Task DisposeAsync()
    {
        foreach (var db in _contexts) await db.DisposeAsync();
        await _connection.DisposeAsync();
    }

    private ContentDbContext Db()
    {
        var db = new ContentDbContext(new DbContextOptionsBuilder<ContentDbContext>().UseSqlite(_connection).Options);
        _contexts.Add(db);
        return db;
    }

    private (GrammarContentProvider Content, GrammarLearningRepository Repo, UnitOfWork Uow, GrammarProgressService Progress) Scope()
    {
        var db = Db();
        var repo = new GrammarLearningRepository(db);
        return (new GrammarContentProvider(_cache, new GrammarContentRepository(db)), repo, new UnitOfWork(db), new GrammarProgressService(repo));
    }

    private SubmitTopicAttemptHandler Submit()
    {
        var s = Scope();
        return new(s.Content, s.Repo, new GrammarResultRecorder(s.Repo), _tokens, s.Uow, _clock,
            NullLogger<SubmitTopicAttemptHandler>.Instance);
    }

    private JsonObject Answers(string topic, string block, params string[] wrong)
    {
        var exercises = (JsonObject)(_data["topics"] as JsonArray)!.OfType<JsonObject>().First(t => t["topic"]!.GetValue<string>() == topic)["exercises"]!;
        var items = block == "mt" ? (JsonArray)exercises["miniTest"]!["questions"]!
            : (JsonArray)(exercises["quickChecks"] as JsonObject)!.First(x => x.Key.EndsWith("." + block[3..])).Value!;
        var answers = new JsonObject();
        foreach (var q in items.OfType<JsonObject>())
        {
            var blanks = q["kind"]!.GetValue<string>() == "cloze6"
                ? (q["subQuestions"] as JsonArray)!.OfType<JsonObject>().Select(s => (s, true)) : [(q, false)];
            foreach (var (item, blank) in blanks)
            {
                var id = item["id"]!.GetValue<string>();
                answers[id] = wrong.Contains(id) ? JsonValue.Create("zzz") : GrammarGrader.CanonicalAnswer(item, blank);
            }
        }
        return answers;
    }

    [Fact]
    public async Task Guest_is_graded_but_nothing_is_stored()
    {
        var result = await Submit().Handle(new SubmitTopicAttemptCommand("qc-1", Answers("04", "qc-1")) { Topic = "04" }, default);
        Assert.False(result.Saved);
        Assert.StartsWith("g.", result.AttemptId);
        Assert.Equal(result.Total, result.Score);
        Assert.Null(result.Best);
        Assert.Equal(0, await Db().GrammarAttempts.CountAsync());
        Assert.Equal(0, await Db().GrammarQuestionResults.CountAsync());
    }

    [Fact]
    public async Task Learner_attempt_records_progress_results_and_fixes()
    {
        var answers = Answers("04", "mt");
        var wrong = answers.Select(a => a.Key).Take(2).ToArray();
        var result = await Submit().Handle(new SubmitTopicAttemptCommand("mt", Answers("04", "mt", wrong))
            { Topic = "04", LearnerId = _learner }, default);
        Assert.True(result.Saved);
        Assert.Equal(result.Total - 2, result.Score);
        Assert.Equal(result.Score, result.Lesson!.MiniTest!.Correct);
        Assert.Null(result.Lesson.QuickCheck);
        Assert.False(result.Results[wrong[0]].Correct);
        Assert.NotNull(result.Results[answers.Select(a => a.Key).Last()].Explanation);

        var s = Scope();
        var retry = new RetryAttemptAnswerHandler(new GrammarAttemptResolver(s.Content, s.Repo, _tokens), new GrammarResultRecorder(s.Repo), s.Uow, _clock);
        var fixedAnswer = await retry.Handle(new RetryAttemptAnswerCommand(wrong[0], answers[wrong[0]]!.DeepClone())
            { AttemptId = result.AttemptId, LearnerId = _learner }, default);
        Assert.True(fixedAnswer.Correct && fixedAnswer.Saved);
        await Assert.ThrowsAsync<ContentRuleException>(() => retry.Handle(new RetryAttemptAnswerCommand(wrong[0], null)
            { AttemptId = result.AttemptId, LearnerId = Guid.NewGuid() }, default));

        var m = Scope();
        var mistakes = await new GetGrammarMistakesHandler(m.Content, m.Repo).Handle(new GetGrammarMistakesQuery(_learner), default);
        var ids = mistakes.SelectMany(g => g.Questions).Select(q => q.QuestionId).ToList();
        Assert.Equal([wrong[1]], ids);
        Assert.StartsWith("CĐ04", mistakes[0].UnitCode);

        var a = Scope();
        var single = await new AnswerQuestionHandler(new GrammarSingleQuestions(a.Content, a.Progress), new GrammarResultRecorder(a.Repo), a.Uow, _clock)
            .Handle(new AnswerQuestionCommand(answers[wrong[1]]!.DeepClone()) { QuestionId = wrong[1], LearnerId = _learner }, default);
        Assert.True(single.Correct);
        var p = Scope();
        var progress = await new GetGrammarProgressHandler(p.Content, p.Progress).Handle(new GetGrammarProgressQuery(_learner), default);
        Assert.Equal(0, progress.Stats.ReviewCount);
        Assert.Equal(44, progress.Lessons.Count);
        Assert.Equal(1, await Db().GrammarAttempts.CountAsync());
    }

    [Fact]
    public async Task Stage_test_is_locked_for_guests_and_unlocks_at_80_percent()
    {
        var s = Scope();
        var stage = new SubmitStageAttemptHandler(s.Content, s.Progress, s.Repo, new GrammarResultRecorder(s.Repo), s.Uow, _clock,
            NullLogger<SubmitStageAttemptHandler>.Instance);
        var locked = await Assert.ThrowsAsync<ContentRuleException>(() => stage.Handle(new SubmitStageAttemptCommand(null) { Stage = "01" }, default));
        Assert.Equal("stage_locked", locked.Code);

        foreach (var topic in new[] { "01", "02", "03", "04", "05", "06", "07", "08" })
        {
            await Submit().Handle(new SubmitTopicAttemptCommand("mt", Answers(topic, "mt")) { Topic = topic, LearnerId = _learner }, default);
            foreach (var part in new[] { "theory", "vocab" })
            {
                var c = Scope();
                var done = await new CompleteTopicPartHandler(c.Content, c.Repo, c.Uow, _clock)
                    .Handle(new CompleteTopicPartCommand(part) { Topic = topic, LearnerId = _learner }, default);
                if (part == "vocab") Assert.True(done.Lesson.Completed);
            }
            if (topic == "07")
            {
                var early = Scope();
                Assert.False((await early.Progress.StageStatusAsync(_learner, await early.Content.GetStageAsync("01", default), default)).Unlocked);
            }
        }
        var u = Scope();
        var status = await u.Progress.StageStatusAsync(_learner, await u.Content.GetStageAsync("01", default), default);
        Assert.Equal(new StageRequirementDto(8, 8, 9), status.Requirement);
        Assert.True(status.Unlocked);
        Assert.False((await u.Progress.StageStatusAsync(null, await u.Content.GetStageAsync("01", default), default)).Unlocked);
    }
}
