using System.Security.Cryptography;
using System.Text;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.CheckAnswers;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.CleanupAttempts;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.RetakeAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.SaveAnswer;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.SetQuestionFlag;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartPracticeSetAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartTestAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.SubmitAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptResult;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptReview;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempts;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetQuestionStatuses;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetsProgress;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeStats;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetTestsProgress;
using ToeicSpace.Assessment.Application.Interfaces.Storage;
using ToeicSpace.Assessment.Application.Services.Attempts;

namespace ToeicSpace.Assessment.Application.UnitTests.Attempts;

/// <summary>
/// Builds attempt handlers over an in-memory database for one learner (switchable via <see cref="User"/>).
/// </summary>
public sealed class AttemptTestHarness : IDisposable
{
    public TestDatabase Db { get; } = new();

    public FakeCurrentUser User { get; set; } = FakeCurrentUser.Learner();

    public FakeAttemptMediaStore Media { get; } = new();

    public AttemptAccess Access => new(Db.Context, User, Db.Time);

    private AttemptContentReader ContentReader => new(Db.Context);

    private AnswerHistoryReader History => new(Db.Context);

    private AttemptDocumentReader Documents => new(ContentReader, History, Access);

    private AttemptGrader Grader => new(Db.Context, Db.Events, TestDatabase.Logger<AttemptGrader>());

    public DateTime Now => Db.Time.GetUtcNow().UtcDateTime;

    public void Advance(TimeSpan span) => Db.Time.Now += span;

    /// <summary>A published standard test (200 questions, one passage per Part 3/4/6/7).</summary>
    public ToeicTest AddPublishedTest()
    {
        var test = Db.AddTest(ContentStatus.Active, code: $"ETS-{Guid.NewGuid():N}"[..20]);
        Db.AddStandardQuestions(test.Id);

        foreach (var passage in Db.Context.ToeicPassages.Where(passage => passage.TestId == test.Id && passage.Part >= ToeicPart.Part6))
        {
            passage.Content = "<p>English text</p><translation_split><p>Bản dịch tiếng Việt</p>";
        }

        Db.Context.SaveChanges();
        return test;
    }

    public Task<AttemptDto> StartTest(Guid testId, AttemptMode mode, IReadOnlyList<int>? parts = null, bool restart = false)
        => new StartTestAttemptHandler(Db.Context, Access, new AttemptSourceReader(Db.Context), Documents, Media,
                TestDatabase.Logger<StartTestAttemptHandler>())
            .Handle(new StartTestAttemptCommand(testId, mode, parts, restart), default);

    public Task<AttemptDto> StartSet(Guid setId, PracticeSelection selection = PracticeSelection.All, int? count = null,
        long generation = 0, bool restart = false)
        => new StartPracticeSetAttemptHandler(Db.Context, Access, new AttemptSourceReader(Db.Context), Documents, History,
                Media, TestDatabase.Logger<StartPracticeSetAttemptHandler>())
            .Handle(new StartPracticeSetAttemptCommand(setId, generation, selection, count, restart), default);

    public Task<SavedAnswerDto> Save(Guid attemptId, Guid questionId, AnswerOption? answer, long expectedRevision = 0,
        Guid? mutationId = null, int? timeSpent = null)
        => new SaveAnswerHandler(Db.Context, Access)
            .Handle(new SaveAnswerCommand(attemptId, questionId, answer, expectedRevision, mutationId ?? Guid.NewGuid(), timeSpent), default);

    public Task<QuestionFlagDto> Flag(Guid attemptId, Guid questionId, bool flagged)
        => new SetQuestionFlagHandler(Db.Context, Access).Handle(new SetQuestionFlagCommand(attemptId, questionId, flagged), default);

    public Task<RevealDto> Check(Guid attemptId, params Guid[] questionIds)
        => new CheckAnswersHandler(Db.Context, Access, ContentReader, History)
            .Handle(new CheckAnswersCommand(attemptId, questionIds), default);

    public Task<AttemptResultDto> Submit(Guid attemptId)
        => new SubmitAttemptHandler(Access, ContentReader, Grader).Handle(new SubmitAttemptCommand(attemptId), default);

    public Task<AttemptDto> Get(Guid attemptId)
        => new GetAttemptHandler(Db.Context, Access, Documents).Handle(new GetAttemptQuery(attemptId), default);

    public Task<AttemptDto> Review(Guid attemptId)
        => new GetAttemptReviewHandler(Db.Context, Access, Documents).Handle(new GetAttemptReviewQuery(attemptId), default);

    public Task<AttemptResultDto> Result(Guid attemptId)
        => new GetAttemptResultHandler(Db.Context, Access, ContentReader).Handle(new GetAttemptResultQuery(attemptId), default);

    public Task<AttemptDto> Retake(Guid attemptId, RetakeScope scope)
        => new RetakeAttemptHandler(Db.Context, Access, ContentReader, Documents, TestDatabase.Logger<RetakeAttemptHandler>())
            .Handle(new RetakeAttemptCommand(attemptId, scope), default);

    public Task<ToeicSpace.Assessment.Application.Models.Attempts.AttemptCleanupResult> Cleanup()
        => new CleanupAttemptsHandler(Db.Context, Access, ContentReader, Grader, TestDatabase.Logger<CleanupAttemptsHandler>())
            .Handle(new CleanupAttemptsCommand(), default);

    public Task<ToeicSpace.BuildingBlocks.Pagination.PaginatedResult<AttemptSummaryDto>> ListAttempts(GetAttemptsQuery query)
        => new GetAttemptsHandler(Access, new AttemptSummaryReader(Db.Context)).Handle(query, default);

    public Task<IReadOnlyList<TestProgressDto>> TestsProgress()
        => new GetTestsProgressHandler(Db.Context, Access).Handle(new GetTestsProgressQuery(), default);

    public Task<IReadOnlyList<PracticeSetProgressDto>> SetsProgress(int? part = null)
        => new GetPracticeSetsProgressHandler(Db.Context, Access, History).Handle(new GetPracticeSetsProgressQuery(part), default);

    public Task<IReadOnlyList<PracticeQuestionStatusDto>> MyQuestions(Guid setId)
        => new GetPracticeSetQuestionStatusesHandler(Db.Context, Access, History)
            .Handle(new GetPracticeSetQuestionStatusesQuery(setId), default);

    public Task<PracticeStatsDto> Stats()
        => new GetPracticeStatsHandler(Db.Context, Access, new AttemptSummaryReader(Db.Context))
            .Handle(new GetPracticeStatsQuery(), default);

    /// <summary>Answers every question of the attempt: correct (B) when <paramref name="correct"/> says so, else A.</summary>
    public async Task AnswerAll(AttemptDto attempt, Func<AttemptQuestionDto, bool> correct)
    {
        foreach (var question in attempt.Questions)
        {
            await Save(attempt.Id, question.Id, correct(question) ? AnswerOption.B : AnswerOption.A);
        }
    }

    public void Dispose() => Db.Dispose();
}

/// <summary>Records preserved URLs and maps them to content-addressed gateway URLs, like the R2 store.</summary>
public sealed class FakeAttemptMediaStore : IAttemptMediaStore
{
    public List<string> Preserved { get; } = [];

    public Task<IReadOnlyDictionary<string, string>> PreserveAsync(IEnumerable<string> urls, CancellationToken cancellationToken)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var url in urls.Distinct(StringComparer.Ordinal))
        {
            Preserved.Add(url);
            map[url] = Retained(url);
        }

        return Task.FromResult<IReadOnlyDictionary<string, string>>(map);
    }

    public Task<(Stream Content, string ContentType)> OpenAsync(string hash, CancellationToken cancellationToken)
        => throw new NotSupportedException();

    public static string Retained(string url)
        => "/assessment/api/v1/practice-media/" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(url))).ToLowerInvariant();
}
