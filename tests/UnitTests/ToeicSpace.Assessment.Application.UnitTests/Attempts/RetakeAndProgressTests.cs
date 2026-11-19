using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempts;

namespace ToeicSpace.Assessment.Application.UnitTests.Attempts;

public sealed class RetakeAndProgressTests
{
    [Fact]
    public async Task RetakeWrong_KeepsWrongAndSkippedQuestions_WithTheirWholePassageGroup()
    {
        using var h = new AttemptTestHarness();
        var testId = h.AddPublishedTest().Id;
        var attempt = await h.StartTest(testId, AttemptMode.Practice, parts: [3, 5]);
        var part3 = attempt.Questions.Where(question => question.Part == 3).ToList();
        var part5 = attempt.Questions.Where(question => question.Part == 5).ToList();
        await h.Save(attempt.Id, part3[0].Id, AnswerOption.A);
        foreach (var question in part3.Skip(1).Concat(part5.Skip(1)))
        {
            await h.Save(attempt.Id, question.Id, AnswerOption.B);
        }
        await h.Submit(attempt.Id);

        var retake = await h.Retake(attempt.Id, RetakeScope.Wrong);

        retake.Mode.Should().Be(AttemptMode.Practice);
        retake.TestId.Should().Be(testId);
        retake.Questions.Select(question => question.SourceQuestionId).Should().Equal(
            part3.Select(question => question.SourceQuestionId).Append(part5[0].SourceQuestionId),
            "the wrong Part 3 answer brings its whole conversation, plus the skipped Part 5 question");
        retake.Passages.Should().ContainSingle();
        retake.Title.Should().EndWith(" · Luyện lại câu sai");
        retake.Questions.Should().OnlyContain(question => question.UserAnswer == null && !question.Checked);
    }

    [Fact]
    public async Task Retake_AbandonsTheInProgressAttemptOnTheSameSource()
    {
        using var h = new AttemptTestHarness();
        var testId = h.AddPublishedTest().Id;
        var completed = await h.StartTest(testId, AttemptMode.Practice, parts: [5]);
        await h.Submit(completed.Id);
        var inProgress = await h.StartTest(testId, AttemptMode.Practice, parts: [6]);

        var retake = await h.Retake(completed.Id, RetakeScope.All);

        retake.Questions.Should().HaveCount(30);
        (await h.Db.Context.ToeicAttempts.SingleAsync(attempt => attempt.Id == inProgress.Id)).Status
            .Should().Be(AttemptStatus.Abandoned);
    }

    [Fact]
    public async Task Retake_RequiresACompletedAttempt_WithSomethingToPractice()
    {
        using var h = new AttemptTestHarness();
        var testId = h.AddPublishedTest().Id;
        var attempt = await h.StartTest(testId, AttemptMode.Practice, parts: [5]);
        var early = () => h.Retake(attempt.Id, RetakeScope.Wrong);
        (await early.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AttemptNotCompleted);

        await h.AnswerAll(attempt, _ => true);
        await h.Submit(attempt.Id);
        var perfect = () => h.Retake(attempt.Id, RetakeScope.Wrong);

        (await perfect.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.NothingToPractice);
    }

    [Fact]
    public async Task Cleanup_AutoSubmitsExpiredMockTests_OnlyAfterTheDelay()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);
        await h.Save(attempt.Id, attempt.Questions[0].Id, AnswerOption.B);

        h.Advance(TimeSpan.FromMinutes(120) + TimeSpan.FromSeconds(30));
        (await h.Cleanup()).Submitted.Should().Be(0);
        h.Advance(TimeSpan.FromSeconds(31));
        (await h.Cleanup()).Submitted.Should().Be(1);

        var result = await h.Result(attempt.Id);
        result.Status.Should().Be(AttemptStatus.Completed);
        result.Score.Correct.Should().Be(1);
        result.DurationSeconds.Should().Be(120 * 60);
    }

    [Fact]
    public async Task Cleanup_DeletesInactivePractice_AndBumpsTheGeneration()
    {
        using var h = new AttemptTestHarness();
        var set = PracticeSelectionTests.AddPart5Set(h, 6);
        var attempt = await h.StartSet(set.Id);

        h.Advance(TimeSpan.FromDays(7));
        var result = await h.Cleanup();

        result.Deleted.Should().Be(1);
        (await h.Db.Context.ToeicAttempts.AnyAsync(item => item.Id == attempt.Id)).Should().BeFalse();
        (await h.Db.Context.PracticeAttemptStates.SingleAsync()).Generation.Should().Be(1);
    }

    [Fact]
    public async Task History_ListsNewestFirst_WithScoresAndFilters()
    {
        using var h = new AttemptTestHarness();
        var testId = h.AddPublishedTest().Id;
        var full = await h.StartTest(testId, AttemptMode.FullTest);
        await h.AnswerAll(full, question => question.Part <= 4);
        await h.Submit(full.Id);
        h.Advance(TimeSpan.FromMinutes(1));
        var practice = await h.StartTest(testId, AttemptMode.Practice, parts: [5]);

        var all = await h.ListAttempts(new GetAttemptsQuery());
        var completed = await h.ListAttempts(new GetAttemptsQuery(Status: AttemptStatus.Completed));

        all.TotalCount.Should().Be(2);
        all.Items.Select(item => item.Id).Should().Equal(practice.Id, full.Id);
        completed.Items.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            Id = full.Id,
            Answered = 200,
            Correct = 100,
            ListeningScaled = 495,
            ReadingScaled = 5,
            TotalScaled = 500
        });
        all.Items[0].ListeningScaled.Should().BeNull();
        all.Items[0].Source.Code.Should().NotBeEmpty();
    }

    [Fact]
    public async Task Progress_ReflectsTheLatestGradedAnswers()
    {
        using var h = new AttemptTestHarness();
        var set = PracticeSelectionTests.AddPart5Set(h, 6);
        var first = await h.StartSet(set.Id);
        await h.Save(first.Id, first.Questions[0].Id, AnswerOption.A);
        await h.Save(first.Id, first.Questions[1].Id, AnswerOption.B);
        await h.Save(first.Id, first.Questions[2].Id, AnswerOption.B);
        await h.Check(first.Id, first.Questions[0].Id, first.Questions[1].Id);

        var sets = await h.SetsProgress(5);
        var questions = await h.MyQuestions(set.Id);

        sets.Should().ContainSingle().Which.Should().BeEquivalentTo(new
        {
            PracticeSetId = set.Id,
            TotalQuestions = 6,
            AnsweredQuestions = 2,
            CorrectQuestions = 1,
            WrongQuestions = 1,
            ActiveAttemptId = (Guid?)first.Id
        });
        questions.Select(question => question.Status).Should().Equal(
            QuestionProgressStatus.Wrong, QuestionProgressStatus.Correct, QuestionProgressStatus.Unanswered,
            QuestionProgressStatus.Unanswered, QuestionProgressStatus.Unanswered, QuestionProgressStatus.Unanswered);
        questions[0].LastAnsweredAt.Should().Be(h.Now);
    }

    [Fact]
    public async Task TestsProgressAndStats_SummariseTheLearner()
    {
        using var h = new AttemptTestHarness();
        var testId = h.AddPublishedTest().Id;
        var full = await h.StartTest(testId, AttemptMode.FullTest);
        await h.AnswerAll(full, question => question.Part >= 5);
        await h.Submit(full.Id);
        h.Advance(TimeSpan.FromMinutes(1));
        var practice = await h.StartTest(testId, AttemptMode.Practice, parts: [1]);
        await h.Save(practice.Id, practice.Questions[0].Id, AnswerOption.B);

        var progress = (await h.TestsProgress()).Should().ContainSingle().Which;
        var stats = await h.Stats();

        progress.Should().BeEquivalentTo(new
        {
            TestId = testId,
            Status = TestProgressStatus.InProgress,
            ActiveAttemptId = (Guid?)practice.Id,
            ActiveMode = (AttemptMode?)AttemptMode.Practice,
            AnsweredCount = 1,
            TotalQuestions = 6,
            CompletedCount = 1,
            BestTotalScaled = (int?)500,
            LastCompletedAttemptId = (Guid?)full.Id
        });
        stats.TestsCompleted.Should().Be(1);
        stats.TestsTotal.Should().Be(1);
        stats.QuestionsAnswered.Should().Be(200);
        stats.CorrectRate.Should().Be(0.5);
        stats.ByPart.Should().HaveCount(7);
        stats.ByPart.Single(part => part.Part == 7).Should().BeEquivalentTo(new { Answered = 54, Correct = 54 });
        stats.ScoreTrend.Should().ContainSingle().Which.TotalScaled.Should().Be(500);
        stats.Recent.Select(item => item.Id).Should().Equal(practice.Id, full.Id);
    }
}
