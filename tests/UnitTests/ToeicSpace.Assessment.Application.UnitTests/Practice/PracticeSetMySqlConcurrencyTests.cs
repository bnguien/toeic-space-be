using Microsoft.EntityFrameworkCore.Diagnostics;
using MySqlConnector;
using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;
using ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;
using ToeicSpace.Assessment.Application.Practice.Common;
using ToeicSpace.Assessment.Application.Questions.Commands.DeleteQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestionStatus;
using ToeicSpace.Assessment.Application.Passages.Commands.UpdatePassage;
using ToeicSpace.Assessment.Application.Passages.Commands.DeletePassage;
using ToeicSpace.Assessment.Application.Passages.Commands.CreatePassage;
using System.Data.Common;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSet;
using ToeicSpace.Assessment.Infrastructure.Persistence.Interceptors;
using ToeicSpace.Assessment.Infrastructure.Persistence;
using ToeicSpace.Assessment.Infrastructure.Messaging.Outbox;
using ToeicSpace.Assessment.Application.Tests.Commands.DeleteTest;

namespace ToeicSpace.Assessment.Application.UnitTests.Practice;

// Opt-in: the supplied connection must be a local disposable MySQL server with CREATE DATABASE rights.
// Each test uses a new schema; no imported records are read or written.
public sealed class PracticeSetMySqlConcurrencyTests
{
    private readonly Xunit.Abstractions.ITestOutputHelper _output;
    public PracticeSetMySqlConcurrencyTests(Xunit.Abstractions.ITestOutputHelper output) => _output = output;
    [MySqlTheory]
    [InlineData("publish", "delete-test")]
    [InlineData("delete-test", "publish")]
    [InlineData("replace", "delete-test")]
    [InlineData("delete-test", "replace")]
    [InlineData("create", "delete-test")]
    [InlineData("delete-test", "create")]
    [InlineData("create-standalone", "delete-test")]
    [InlineData("delete-test", "create-standalone")]
    [InlineData("create-passage", "delete-test")]
    [InlineData("delete-test", "create-passage")]
    public async Task TestDeletion_CoordinatesWithPublishingMembershipAndNewQuestions(string first, string second)
    {
        await using var database = await MySqlDatabase.CreateAsync(withPassage: true, withTest: true);
        var pause = new PauseBeforeSave();
        await using var left = database.Context(pause);
        await using var right = database.Context();
        var firstTask = Capture(() => Operate(left, database, first));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var secondTask = Capture(() => Operate(right, database, second));
        try { await Task.WhenAny(secondTask, Task.Delay(300)); }
        finally { pause.Release.TrySetResult(); }
        var errors = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(25));
        errors.Should().OnlyContain(error => error == null || error is AppException);
        await AssertValid(database);
        await using var check = database.Context();
        var passageIds = await check.ToeicPassages.Select(p => p.Id).ToListAsync();
        (await check.ToeicQuestions.Where(q => q.PassageId.HasValue)
            .AllAsync(q => passageIds.Contains(q.PassageId!.Value))).Should().BeTrue();
        var testExists = await check.ToeicTests.AnyAsync(t => t.Id == database.TestId);
        if (!testExists)
        {
            (await check.ToeicQuestions.AnyAsync(q => q.TestId == database.TestId)).Should().BeFalse();
            (await check.ToeicPassages.AnyAsync(p => p.TestId == database.TestId)).Should().BeFalse();
        }
    }

    [MySqlTheory]
    [InlineData("publish", "archive", false)]
    [InlineData("archive", "publish", false)]
    [InlineData("publish", "delete", false)]
    [InlineData("delete", "publish", false)]
    [InlineData("publish", "replace", false)]
    [InlineData("replace", "publish", false)]
    [InlineData("publish", "unpublish", false)]
    [InlineData("unpublish", "publish", false)]
    [InlineData("replace", "archive", true)]
    [InlineData("replace", "delete", true)]
    [InlineData("publish", "passage", false)]
    [InlineData("passage", "publish", false)]
    [InlineData("replace", "passage", true)]
    [InlineData("publish", "question", false)]
    [InlineData("question", "publish", false)]
    public async Task OverlappingWrites_PreserveThePublishedInvariant(string first, string second, bool active)
    {
        await using var database = await MySqlDatabase.CreateAsync(withPassage: true, active);
        var pause = new PauseBeforeSave();
        await using var left = database.Context(pause);
        await using var right = database.Context();
        var firstTask = Capture(() => Operate(left, database, first));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var secondTask = Capture(() => Operate(right, database, second));
        try { await Task.WhenAny(secondTask, Task.Delay(300)); }
        finally { pause.Release.TrySetResult(); }
        var errors = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(25));
        errors.Should().OnlyContain(error => error == null || error is AppException,
            "deadlocks, lock timeouts and retry exhaustion must not escape the protocol");
        await AssertValid(database);
    }

    [MySqlTheory]
    [InlineData("create", "delete-passage")]
    [InlineData("delete-passage", "create")]
    public async Task QuestionCreation_AndPassageDeletion_CannotCreateAnOrphan(string first, string second)
    {
        await using var database = await MySqlDatabase.CreateAsync();
        var pause = new PauseBeforeSave();
        await using var left = database.Context(pause);
        await using var right = database.Context();
        var firstTask = Capture(() => Operate(left, database, first));
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var secondTask = Capture(() => Operate(right, database, second));
        try { await Task.WhenAny(secondTask, Task.Delay(300)); }
        finally { pause.Release.TrySetResult(); }
        var errors = await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(25));
        errors.Should().OnlyContain(error => error == null || error is AppException);
        await using var check = database.Context();
        var passageExists = await check.ToeicPassages.AnyAsync(p => p.Id == database.PassageId);
        var hasQuestions = await check.ToeicQuestions.AnyAsync(q => q.PassageId == database.PassageId);
        (passageExists || !hasQuestions).Should().BeTrue();
    }

    [MySqlFact]
    public async Task IndependentSets_DoNotWaitForUnrelatedQuestionLocks()
    {
        await using var database = await MySqlDatabase.CreateAsync();
        var otherId = Guid.NewGuid();
        await using (var seed = database.Context())
        {
            seed.ToeicPracticeSets.Add(new ToeicPracticeSet
            {
                Id = otherId,
                Code = "independent",
                Title = "Independent",
                Part = ToeicPart.Part5,
                Kind = PracticeSetKind.Topic,
                Status = ContentStatus.Draft
            });
            seed.ToeicPracticeSetItems.Add(new ToeicPracticeSetItem
            {
                PracticeSetId = otherId,
                QuestionId = database.OtherQuestionId
            });
            await seed.SaveChangesAsync();
        }
        var pause = new PauseBeforeSave();
        await using var left = database.Context(pause);
        await using var right = database.Context();
        var first = Status(left, database.SetId, ContentStatus.Active);
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        try { await Status(right, otherId, ContentStatus.Active).WaitAsync(TimeSpan.FromSeconds(10)); }
        finally { pause.Release.TrySetResult(); }
        await first.WaitAsync(TimeSpan.FromSeconds(15));
    }

    [MySqlFact]
    public async Task RepeatedConcurrentSetWrites_AndBankWrites_DoNotExhaustRetries()
    {
        await using var database = await MySqlDatabase.CreateAsync(withPassage: true);
        for (var iteration = 0; iteration < 5; iteration++)
        {
            var errors = await Task.WhenAll(new[] { "publish", "replace", "archive", "unpublish", "passage" }
                .Select(async operation =>
                {
                    await using var context = database.Context();
                    return await Capture(() => Operate(context, database, operation));
                })).WaitAsync(TimeSpan.FromSeconds(25));
            errors.Should().OnlyContain(error => error == null || error is AppException);
            await AssertValid(database);
        }
    }

    [MySqlFact]
    public async Task InvalidBankEdits_RollBackContentAndVersion_WhileValidEditsRemainAllowed()
    {
        await using var database = await MySqlDatabase.CreateAsync(withPassage: true, active: true);
        await using var context = database.Context();
        var error = await Capture(() => Operate(context, database, "passage"));
        error.Should().BeOfType<AppException>();
        context.ChangeTracker.Clear();
        (await context.ToeicPassages.SingleAsync()).AudioUrl.Should().NotBeNull();
        error = await Capture(() => Operate(context, database, "question"));
        error.Should().BeOfType<AppException>();
        context.ChangeTracker.Clear();
        var question = await context.ToeicQuestions.SingleAsync(q => q.Id == database.QuestionId);
        question.QuestionText.Should().Be("Choose the answer.");
        question.Version.Should().Be(1);
        (await context.OutboxMessages.CountAsync()).Should().Be(0, "failed validation must roll back its outbox event too");
        await new UpdatePassageHandler(context, Cache(context)).Handle(new(
            database.PassageId, null, ToeicPart.Part3, null, null, "Valid updated script",
            "https://example.com/audio.mp3", null, null), default);
        await AssertValid(database);
    }

    private static async Task AssertValid(MySqlDatabase database)
    {
        await using var context = database.Context();
        var set = await context.ToeicPracticeSets.SingleAsync();
        if (set.Status != ContentStatus.Active) return;
        var ids = await context.ToeicPracticeSetItems.Where(i => i.PracticeSetId == set.Id)
            .Select(i => i.QuestionId).ToListAsync();
        await PracticeSetPublishingGuard.EnsureValidAsync(context, set, ids, default, lockContent: false);
    }

    private static async Task Operate(AssessmentDbContext context, MySqlDatabase database, string operation)
    {
        switch (operation)
        {
            case "publish": await Status(context, database.SetId, ContentStatus.Active); break;
            case "unpublish": await Status(context, database.SetId, ContentStatus.Archived); break;
            case "archive":
                await new UpdateQuestionStatusHandler(context, Events(context), Cache(context), TimeProvider.System)
                    .Handle(new(database.QuestionId, ContentStatus.Archived), default); break;
            case "delete":
                await new DeleteQuestionHandler(context, Events(context), Cache(context), TimeProvider.System,
                    TestDatabase.Logger<DeleteQuestionHandler>()).Handle(new(database.QuestionId), default); break;
            case "replace":
                await new ReplacePracticeSetItemsHandler(context, FakeCurrentUser.Admin(), Cache(context))
                    .Handle(new(database.SetId, [database.OtherQuestionId], [database.QuestionId]), default); break;
            case "passage":
                await new UpdatePassageHandler(context, Cache(context)).Handle(new(
                    database.PassageId, null, ToeicPart.Part3, null, null, "Script", null, null, null), default); break;
            case "question":
                await new UpdateQuestionHandler(context, new QuestionReferenceGuard(context), Events(context),
                    Cache(context), TimeProvider.System, TestDatabase.Logger<UpdateQuestionHandler>()).Handle(new(
                        database.QuestionId, 1, null, database.PassageId, ToeicPart.Part3, null, null, null, null,
                        "A", "B", "C", "D", AnswerOption.A, null, null), default); break;
            case "create":
                await new CreateQuestionHandler(context, new QuestionReferenceGuard(context), Events(context),
                    Cache(context), FakeCurrentUser.Admin(), TimeProvider.System, TestDatabase.Logger<CreateQuestionHandler>())
                    .Handle(new(database.TestId, database.PassageId, ToeicPart.Part3, database.TestId.HasValue ? 34 : null, "New question", null, null,
                        "A", "B", "C", "D", AnswerOption.A, null, null), default); break;
            case "create-standalone":
                await new CreateQuestionHandler(context, new QuestionReferenceGuard(context), Events(context),
                    Cache(context), FakeCurrentUser.Admin(), TimeProvider.System, TestDatabase.Logger<CreateQuestionHandler>())
                    .Handle(new(database.TestId, null, ToeicPart.Part5, 101, "Standalone", null, null,
                        "A", "B", "C", "D", AnswerOption.A, null, null), default); break;
            case "create-passage":
                await new CreatePassageHandler(context).Handle(new(database.TestId, ToeicPart.Part3,
                    null, null, "New passage", "https://example.com/audio.mp3", null, null), default); break;
            case "delete-passage":
                await new DeletePassageHandler(context, Cache(context), TimeProvider.System)
                    .Handle(new(database.PassageId), default); break;
            case "delete-test":
                await new DeleteTestHandler(context, Events(context), Cache(context), TimeProvider.System,
                    TestDatabase.Logger<DeleteTestHandler>()).Handle(new(database.TestId!.Value), default); break;
            default: throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    [MySqlFact]
    public async Task Publish_AndArchive_CannotCommitAnInvalidActiveSet()
    {
        await using var database = await MySqlDatabase.CreateAsync();
        var pause = new PauseBeforeSave();
        await using var publisher = database.Context(pause);
        await using var bank = database.Context();
        var publish = Status(publisher, database.SetId, ContentStatus.Active);
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var archive = Capture(async () => await new UpdateQuestionStatusHandler(
            bank, Events(bank), Cache(bank), TimeProvider.System)
            .Handle(new(database.QuestionId, ContentStatus.Archived), default));
        try
        {
            // Give the independent connection time to reach the guarded write while publishing is paused.
            await Task.WhenAny(archive, Task.Delay(300));
        }
        finally
        {
            pause.Release.TrySetResult();
        }
        await publish.WaitAsync(TimeSpan.FromSeconds(20));
        var error = await archive.WaitAsync(TimeSpan.FromSeconds(20));
        await using var check = database.Context();
        var set = await check.ToeicPracticeSets.SingleAsync();
        var question = await check.ToeicQuestions.SingleAsync(q => q.Id == database.QuestionId);
        (set.Status != ContentStatus.Active || question.Status == ContentStatus.Active)
            .Should().BeTrue("publishing and archiving must share a transaction protocol");
        error.Should().BeOfType<AppException>();
    }

    private static Task Status(AssessmentDbContext context, Guid id, ContentStatus status) =>
        new UpdatePracticeSetStatusHandler(context, FakeCurrentUser.Admin(), Cache(context))
            .Handle(new(id, status), default);

    private static ContentCacheInvalidator Cache(AssessmentDbContext context) => new(context, new InMemoryContentCache());

    private static OutboxIntegrationEventPublisher Events(AssessmentDbContext context) => new(context, TimeProvider.System);

    private static async Task<Exception?> Capture(Func<Task> action)
    {
        try { await action(); return null; }
        catch (Exception error) { return error; }
    }

    [MySqlFact]
    public async Task TwoThousandQuestions_UseTwoParameterizedSortedLockQueries()
    {
        await using var database = await MySqlDatabase.CreateAsync(withPassage: true);
        var counter = new LockQueryCounter();
        await using var context = database.Context(counter);
        var ids = Enumerable.Range(0, 1999).Select(_ => Guid.NewGuid()).Append(database.QuestionId).ToArray();
        await using (var seed = database.Context())
        {
            seed.ToeicQuestions.AddRange(ids.Where(id => id != database.QuestionId).Select(id => new ToeicQuestion
            {
                Id = id,
                PassageId = database.PassageId,
                Part = ToeicPart.Part3,
                QuestionText = "Performance fixture",
                OptionA = "A",
                OptionB = "B",
                OptionC = "C",
                OptionD = "D",
                CorrectAnswer = AnswerOption.A,
                Status = ContentStatus.Active,
                Version = 1
            }));
            await seed.SaveChangesAsync();
        }
        var timer = System.Diagnostics.Stopwatch.StartNew();
        await context.ExecutePracticeSetUpdateAsync(database.SetId, async () =>
        {
            var set = await context.ToeicPracticeSets.AsNoTracking().SingleAsync();
            await PracticeSetPublishingGuard.EnsureValidAsync(context, set, ids.Reverse().ToArray(), default);
            return true;
        }, default);
        counter.ContentLocks.Should().HaveCount(2);
        counter.ContentLocks.Should().OnlyContain(sql => sql.Contains(" IN (") && sql.Contains("ORDER BY Id FOR UPDATE"));
        counter.Reads.Should().Be(6); // set lock, set load, question/passage locks, question/passage loads.
        _output.WriteLine($"Validated and locked 2,000 real questions sharing one passage in {timer.ElapsedMilliseconds} ms; {counter.Reads} read queries, 2 content lock batches.");
    }

    [MySqlFact]
    public async Task ConcurrentMetadataEdits_WithSameBaseline_RejectTheStaleWriter()
    {
        await using var database = await MySqlDatabase.CreateAsync();
        await using var read = database.Context();
        var set = await read.ToeicPracticeSets.AsNoTracking().SingleAsync();
        var command = new UpdatePracticeSetCommand(set.Id, set.Code, "First writer", null,
            set.Kind, set.Part, null, null, 30, ExpectedUpdatedAt: set.UpdatedAt ?? set.CreatedAt);
        var pause = new PauseBeforeSave();
        await using var left = database.Context(new AuditableEntityInterceptor(TimeProvider.System), pause);
        await using var right = database.Context(new AuditableEntityInterceptor(TimeProvider.System));
        var first = new UpdatePracticeSetHandler(left, FakeCurrentUser.Admin(), Cache(left)).Handle(command, default);
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var second = Capture(() => new UpdatePracticeSetHandler(right, FakeCurrentUser.Admin(), Cache(right))
            .Handle(command with { Title = "Stale writer" }, default));
        try { await Task.WhenAny(second, Task.Delay(300)); }
        finally { pause.Release.TrySetResult(); }
        var saved = await first.WaitAsync(TimeSpan.FromSeconds(25));
        var error = await second.WaitAsync(TimeSpan.FromSeconds(25));
        error.Should().BeOfType<AppException>().Which.Code.Should().Be(ErrorCodes.ConcurrencyConflict);
        // The immediately returned save timestamp is also usable after MySQL precision truncation.
        await new UpdatePracticeSetHandler(right, FakeCurrentUser.Admin(), Cache(right)).Handle(
            command with { Title = "Fresh writer", ExpectedUpdatedAt = saved.UpdatedAt ?? saved.CreatedAt }, default);
    }

    [MySqlFact]
    public async Task ConcurrentQuestionLists_WithSameBaseline_RejectTheStaleWriter()
    {
        await using var database = await MySqlDatabase.CreateAsync();
        var baseline = new[] { database.QuestionId };
        var pause = new PauseBeforeSave();
        await using var left = database.Context(pause);
        await using var right = database.Context();
        var first = new ReplacePracticeSetItemsHandler(left, FakeCurrentUser.Admin(), Cache(left))
            .Handle(new(database.SetId, [database.OtherQuestionId], baseline), default);
        await pause.Entered.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var second = Capture(() => new ReplacePracticeSetItemsHandler(right, FakeCurrentUser.Admin(), Cache(right))
            .Handle(new(database.SetId, [database.QuestionId, database.OtherQuestionId], baseline), default));
        try { await Task.WhenAny(second, Task.Delay(300)); }
        finally { pause.Release.TrySetResult(); }
        await first.WaitAsync(TimeSpan.FromSeconds(25));
        var error = await second.WaitAsync(TimeSpan.FromSeconds(25));
        error.Should().BeOfType<AppException>().Which.Code.Should().Be(ErrorCodes.ConcurrencyConflict);
        await using var check = database.Context();
        (await check.ToeicPracticeSetItems.OrderBy(item => item.OrderIndex).Select(item => item.QuestionId).ToListAsync())
            .Should().Equal(database.OtherQuestionId);
    }

    private sealed class LockQueryCounter : DbCommandInterceptor
    {
        public List<string> ContentLocks { get; } = [];
        public int Reads { get; private set; }
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            Reads++;
            if (command.CommandText.Contains("FOR UPDATE") && !command.CommandText.Contains("ToeicPracticeSets"))
                ContentLocks.Add(command.CommandText);
            return ValueTask.FromResult(result);
        }
    }

    private sealed class PauseBeforeSave : SaveChangesInterceptor
    {
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(30), cancellationToken);
            return result;
        }
    }

    private sealed class MySqlDatabase : IAsyncDisposable
    {
        private readonly string _connection;
        public Guid SetId { get; } = Guid.NewGuid();
        public Guid QuestionId { get; } = Guid.NewGuid();
        public Guid OtherQuestionId { get; } = Guid.NewGuid();
        public Guid PassageId { get; } = Guid.NewGuid();
        public Guid? TestId { get; private set; }

        private MySqlDatabase(string connection) => _connection = connection;

        public AssessmentDbContext Context(params IInterceptor[] interceptors) => new(
            new DbContextOptionsBuilder<AssessmentDbContext>()
                .UseMySql(_connection, new MySqlServerVersion(new Version(8, 4, 0)),
                    options => options.EnableRetryOnFailure(3))
                .AddInterceptors(interceptors).Options);

        public static async Task<MySqlDatabase> CreateAsync(bool withPassage = false, bool active = false, bool withTest = false)
        {
            var builder = new MySqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PBL26_MYSQL_CONNECTION")!);
            if (builder.Server is not ("127.0.0.1" or "localhost"))
                throw new InvalidOperationException("Concurrency tests require a local MySQL server.");
            builder.Database = "pbl26_review_" + Guid.NewGuid().ToString("N");
            var database = new MySqlDatabase(builder.ConnectionString);
            await using var context = database.Context();
            await context.Database.EnsureCreatedAsync();
            if (withTest)
            {
                database.TestId = Guid.NewGuid();
                context.ToeicTests.Add(new ToeicTest
                {
                    Id = database.TestId.Value,
                    Code = "fixture-test",
                    Title = "Fixture",
                    Status = ContentStatus.Draft
                });
            }
            context.ToeicPracticeSets.Add(new ToeicPracticeSet
            {
                Id = database.SetId,
                Code = builder.Database,
                Title = "Concurrency fixture",
                Part = withPassage ? ToeicPart.Part3 : ToeicPart.Part5,
                Kind = PracticeSetKind.Topic,
                Status = active ? ContentStatus.Active : ContentStatus.Draft
            });
            context.ToeicPassages.Add(new ToeicPassage
            {
                Id = database.PassageId,
                TestId = database.TestId,
                Part = ToeicPart.Part3,
                Content = "Script",
                AudioUrl = "https://example.com/audio.mp3"
            });
            context.ToeicQuestions.Add(new ToeicQuestion
            {
                Id = database.QuestionId,
                Part = withPassage ? ToeicPart.Part3 : ToeicPart.Part5,
                TestId = database.TestId,
                QuestionNumber = withTest ? 32 : null,
                PassageId = withPassage ? database.PassageId : null,
                QuestionText = "Choose the answer.",
                OptionA = "A",
                OptionB = "B",
                OptionC = "C",
                OptionD = "D",
                CorrectAnswer = AnswerOption.A,
                Status = ContentStatus.Active,
                Version = 1
            });
            context.ToeicQuestions.Add(new ToeicQuestion
            {
                Id = database.OtherQuestionId,
                Part = withPassage ? ToeicPart.Part3 : ToeicPart.Part5,
                TestId = database.TestId,
                QuestionNumber = withTest ? 33 : null,
                PassageId = withPassage ? database.PassageId : null,
                QuestionText = "Another question.",
                OptionA = "A",
                OptionB = "B",
                OptionC = "C",
                OptionD = "D",
                CorrectAnswer = AnswerOption.A,
                Status = ContentStatus.Active,
                Version = 1
            });
            context.ToeicPracticeSetItems.Add(new ToeicPracticeSetItem
            {
                PracticeSetId = database.SetId,
                QuestionId = database.QuestionId,
                OrderIndex = 0
            });
            await context.SaveChangesAsync();
            return database;
        }

        public async ValueTask DisposeAsync()
        {
            if (!new MySqlConnectionStringBuilder(_connection).Database.StartsWith("pbl26_review_", StringComparison.Ordinal))
                throw new InvalidOperationException("Refusing cleanup outside the disposable test schema.");
            await using var context = Context();
            await context.Database.EnsureDeletedAsync();
        }
    }

    private sealed class MySqlFactAttribute : FactAttribute
    {
        public MySqlFactAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PBL26_MYSQL_CONNECTION")))
                Skip = "Set PBL26_MYSQL_CONNECTION to opt into isolated local MySQL integration tests.";
        }
    }

    private sealed class MySqlTheoryAttribute : TheoryAttribute
    {
        public MySqlTheoryAttribute()
        {
            if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("PBL26_MYSQL_CONNECTION")))
                Skip = "Set PBL26_MYSQL_CONNECTION to opt into isolated local MySQL integration tests.";
        }
    }
}
