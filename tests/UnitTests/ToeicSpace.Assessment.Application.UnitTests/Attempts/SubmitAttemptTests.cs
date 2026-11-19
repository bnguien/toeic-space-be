using ToeicSpace.Assessment.Domain.Rules;
using ToeicSpace.BuildingBlocks.Messaging.Events;

namespace ToeicSpace.Assessment.Application.UnitTests.Attempts;

public sealed class SubmitAttemptTests
{
    [Fact]
    public async Task FullTest_IsGradedFromTheSnapshot_WithScaledScoresAndCefr()
    {
        using var h = new AttemptTestHarness();
        var test = h.AddPublishedTest();
        var attempt = await h.StartTest(test.Id, AttemptMode.FullTest);
        // Listening: questions 1–80 correct (80/100); Reading: 101–160 correct (60/100); 161–200 unanswered.
        foreach (var question in attempt.Questions.Where(question => question.QuestionNumber <= 160))
        {
            var correct = question.QuestionNumber <= 80 || question.QuestionNumber > 100;
            await h.Save(attempt.Id, question.Id, correct ? AnswerOption.B : AnswerOption.A);
        }

        // The bank changes after the start: grading must use the snapshot answer key.
        foreach (var question in h.Db.Context.ToeicQuestions)
        {
            question.CorrectAnswer = AnswerOption.C;
        }
        await h.Db.Context.SaveChangesAsync();
        h.Advance(TimeSpan.FromMinutes(95));

        var result = await h.Submit(attempt.Id);

        var listening = ToeicScoreConversion.ScaledScore(80, 100);
        var reading = ToeicScoreConversion.ScaledScore(60, 100);
        result.Status.Should().Be(AttemptStatus.Completed);
        result.Score.Should().BeEquivalentTo(new
        {
            QuestionCount = 200,
            Answered = 160,
            Correct = 140,
            ListeningCorrect = 80,
            ListeningTotal = 100,
            ReadingCorrect = 60,
            ReadingTotal = 100,
            ListeningScaled = listening,
            ReadingScaled = reading,
            TotalScaled = listening + reading
        });
        result.Score.Cefr!.Level.Should().Be(ToeicScoreConversion.Cefr(listening + reading).Level);
        result.ByPart.Should().HaveCount(7);
        result.ByPart.Single(part => part.Part == 7).Should().BeEquivalentTo(new { Total = 54, Answered = 14, Correct = 14 });
        result.Answers.Single(answer => answer.QuestionNumber == 200).IsCorrect.Should().BeNull();
        result.Answers.Single(answer => answer.QuestionNumber == 90).IsCorrect.Should().BeFalse();
        result.DurationSeconds.Should().Be(95 * 60);
        var published = h.Db.Events.Events.OfType<ExamAttemptSubmittedIntegrationEvent>().Should().ContainSingle().Which;
        published.TotalScore.Should().Be(listening + reading);
        published.TestId.Should().Be(test.Id);
    }

    [Fact]
    public async Task Submit_IsIdempotent()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);
        await h.Save(attempt.Id, attempt.Questions[0].Id, AnswerOption.B);

        var first = await h.Submit(attempt.Id);
        h.Advance(TimeSpan.FromMinutes(1));
        var second = await h.Submit(attempt.Id);

        second.Should().BeEquivalentTo(first);
        h.Db.Events.Events.Should().ContainSingle();
    }

    [Fact]
    public async Task LateFullTest_IsAccepted_AndItsDurationStopsAtTheDeadline()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);
        h.Advance(TimeSpan.FromMinutes(150));

        var result = await h.Submit(attempt.Id);

        result.DurationSeconds.Should().Be(120 * 60);
        result.Score.Answered.Should().Be(0);
        result.Score.TotalScaled.Should().Be(10);
    }

    [Fact]
    public async Task PartialPractice_HasNoScaledScore()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [5]);
        await h.AnswerAll(attempt, _ => true);

        var result = await h.Submit(attempt.Id);

        result.Score.Correct.Should().Be(30);
        result.Score.ListeningScaled.Should().BeNull();
        result.Score.ReadingScaled.Should().BeNull();
        result.Score.TotalScaled.Should().BeNull();
        result.Score.Cefr.Should().BeNull();
        h.Db.Events.Events.Should().BeEmpty("only mock tests are published");
    }

    [Fact]
    public async Task ListeningPractice_HasAListeningScoreOnly()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [1, 2, 3, 4]);
        await h.AnswerAll(attempt, _ => true);

        var result = await h.Submit(attempt.Id);

        result.Score.ListeningScaled.Should().Be(495);
        result.Score.ReadingScaled.Should().BeNull();
        result.Score.TotalScaled.Should().BeNull();
    }

    [Fact]
    public async Task PracticeDuration_IsTheReportedTimeSpent()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [5]);
        await h.Save(attempt.Id, attempt.Questions[0].Id, AnswerOption.B, timeSpent: 40);
        await h.Save(attempt.Id, attempt.Questions[1].Id, AnswerOption.B, timeSpent: 25);
        h.Advance(TimeSpan.FromHours(3));

        (await h.Submit(attempt.Id)).DurationSeconds.Should().Be(65);
    }

    [Fact]
    public async Task ResultAndReview_RequireACompletedAttempt()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);

        var result = () => h.Result(attempt.Id);
        var review = () => h.Review(attempt.Id);

        (await result.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AttemptNotCompleted);
        (await review.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AttemptNotCompleted);
    }

    [Fact]
    public async Task Review_RevealsEverything_WithAnswersHistoryAndScripts()
    {
        using var h = new AttemptTestHarness();
        var testId = h.AddPublishedTest().Id;
        var first = await h.StartTest(testId, AttemptMode.Practice, parts: [3]);
        await h.AnswerAll(first, _ => false);
        await h.Submit(first.Id);
        h.Advance(TimeSpan.FromMinutes(5));
        var second = await h.StartTest(testId, AttemptMode.Practice, parts: [3]);
        await h.AnswerAll(second, _ => true);
        await h.Submit(second.Id);

        var review = await h.Review(second.Id);

        review.Reveal.Questions.Should().HaveCount(39).And.OnlyContain(question => question.IsCorrect == true);
        review.Reveal.Questions[0].History.Should().ContainSingle()
            .Which.Should().BeEquivalentTo(new { AttemptId = first.Id, UserAnswer = AnswerOption.A, IsCorrect = false });
        review.Reveal.Passages.Single().Content.Should().Be("W: Hello. M: Hi.");
        review.Passages.Single().Content.Should().Be("W: Hello. M: Hi.");
        review.Questions[0].UserAnswer.Should().Be(AnswerOption.B, "review keeps the attempt's own answers");
    }

    [Fact]
    public async Task OtherLearners_CannotSeeAnAttempt()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);
        h.User = FakeCurrentUser.Learner();

        var get = () => h.Get(attempt.Id);
        var submit = () => h.Submit(attempt.Id);

        (await get.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AttemptUnavailable);
        (await submit.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.NotFound);
    }
}
