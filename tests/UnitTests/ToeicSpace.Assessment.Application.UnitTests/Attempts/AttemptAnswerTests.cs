using ToeicSpace.Assessment.Application.Features.Attempts.Commands.CheckAnswers;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.SaveAnswer;

namespace ToeicSpace.Assessment.Application.UnitTests.Attempts;

public sealed class AttemptAnswerTests
{
    [Fact]
    public async Task Save_AddsTimeSpent_AndRejectsAStaleRevision()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [5]);
        var question = attempt.Questions[0];

        var first = await h.Save(attempt.Id, question.Id, AnswerOption.A, expectedRevision: 0, timeSpent: 12);
        var second = await h.Save(attempt.Id, question.Id, AnswerOption.C, expectedRevision: 1, timeSpent: 8);
        var stale = () => h.Save(attempt.Id, question.Id, AnswerOption.D, expectedRevision: 1);

        first.Revision.Should().Be(1);
        second.Revision.Should().Be(2);
        second.TimeSpentSeconds.Should().Be(20);
        (await stale.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AnswerRevisionConflict);
    }

    [Fact]
    public async Task Save_RetriedMutation_IsAppliedOnce()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [5]);
        var question = attempt.Questions[0];
        var mutation = Guid.NewGuid();

        await h.Save(attempt.Id, question.Id, AnswerOption.A, 0, mutation, timeSpent: 30);
        var retry = await h.Save(attempt.Id, question.Id, AnswerOption.A, 0, mutation, timeSpent: 30);
        var reused = () => h.Save(attempt.Id, question.Id, AnswerOption.C, 1, mutation);

        retry.Revision.Should().Be(1);
        retry.TimeSpentSeconds.Should().Be(30);
        (await reused.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.MutationConflict);
    }

    [Fact]
    public async Task Save_FullTest_IsRejectedAfterTheDeadlineGrace()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);
        var question = attempt.Questions[0];

        h.Advance(TimeSpan.FromMinutes(120) + TimeSpan.FromSeconds(20));
        await h.Save(attempt.Id, question.Id, AnswerOption.B);
        h.Advance(TimeSpan.FromSeconds(15));
        var late = () => h.Save(attempt.Id, question.Id, AnswerOption.C, expectedRevision: 1);

        (await late.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AttemptTimeUp);
    }

    [Fact]
    public async Task Save_OnASubmittedAttempt_IsNotActive()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [5]);
        await h.Submit(attempt.Id);

        var save = () => h.Save(attempt.Id, attempt.Questions[0].Id, AnswerOption.B);

        (await save.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AttemptNotActive);
    }

    [Fact]
    public async Task Save_InvalidOptionForPart2_IsRejected()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [2]);

        var save = () => h.Save(attempt.Id, attempt.Questions[0].Id, AnswerOption.D);

        (await save.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public async Task Flag_IsStoredWithoutChangingTheAnswerRevision()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);
        var question = attempt.Questions[3];

        var flag = await h.Flag(attempt.Id, question.Id, true);
        await h.Save(attempt.Id, question.Id, AnswerOption.B, expectedRevision: 0);
        var reloaded = await h.Get(attempt.Id);

        flag.Should().Be(new ToeicSpace.Assessment.Application.DTOs.Attempts.QuestionFlagDto(question.Id, true));
        reloaded.Questions[3].Flagged.Should().BeTrue();
        reloaded.Questions[3].Revision.Should().Be(1);
    }

    [Fact]
    public async Task Check_IsForbiddenInAMockTest()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.FullTest);

        var check = () => h.Check(attempt.Id, attempt.Questions[0].Id);

        var error = (await check.Should().ThrowAsync<AppException>()).Which;
        error.Type.Should().Be(ErrorType.Forbidden);
        error.Code.Should().Be(ErrorCodes.CheckNotAllowed);
    }

    [Fact]
    public async Task Check_RevealsAndLocksTheGroup_IncludingItsPassage()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [6]);
        var answered = attempt.Questions[0];
        var skipped = attempt.Questions[1];
        await h.Save(attempt.Id, answered.Id, AnswerOption.A);

        var reveal = await h.Check(attempt.Id, answered.Id, skipped.Id);
        var locked = () => h.Save(attempt.Id, answered.Id, AnswerOption.B, expectedRevision: 1);
        var reloaded = await h.Get(attempt.Id);

        reveal.Questions.Should().HaveCount(2);
        reveal.Questions[0].Should().Match<ToeicSpace.Assessment.Application.DTOs.Attempts.QuestionRevealDto>(question =>
            question.CorrectAnswer == AnswerOption.B && question.IsCorrect == false && question.Explanation == "Because B.");
        reveal.Questions[1].IsCorrect.Should().BeNull("an unanswered question is skipped");
        reveal.Passages.Should().ContainSingle().Which.Translation.Should().Be("<p>Bản dịch tiếng Việt</p>");
        (await locked.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.AnswerLocked);
        reloaded.Questions.Where(question => question.Checked).Should().HaveCount(2);
        reloaded.Reveal.Questions.Select(question => question.QuestionId).Should().Equal(answered.Id, skipped.Id);
        reloaded.Passages.Single().Content.Should().Be("<p>English text</p>", "passages stay English-only in progress");
    }

    [Fact]
    public async Task Check_IsIdempotent()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [5]);
        var question = attempt.Questions[0];
        await h.Save(attempt.Id, question.Id, AnswerOption.B);
        await h.Check(attempt.Id, question.Id);
        var checkedAt = (await h.Db.Context.ToeicAttemptAnswers.SingleAsync()).CheckedAt;

        h.Advance(TimeSpan.FromMinutes(5));
        var again = await h.Check(attempt.Id, question.Id);

        again.Questions.Single().IsCorrect.Should().BeTrue();
        (await h.Db.Context.ToeicAttemptAnswers.SingleAsync()).CheckedAt.Should().Be(checkedAt);
    }

    [Fact]
    public async Task Check_RejectsQuestionsOfAnotherAttempt()
    {
        using var h = new AttemptTestHarness();
        var attempt = await h.StartTest(h.AddPublishedTest().Id, AttemptMode.Practice, parts: [5]);

        var check = () => h.Check(attempt.Id, Guid.NewGuid());

        (await check.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.Validation);
    }

    [Fact]
    public void Validators_BoundTimeSpentAndCheckSize()
    {
        new SaveAnswerValidator().Validate(new SaveAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), AnswerOption.A, 0,
            Guid.NewGuid(), 3601)).IsValid.Should().BeFalse();
        new SaveAnswerValidator().Validate(new SaveAnswerCommand(Guid.NewGuid(), Guid.NewGuid(), null, 0,
            Guid.NewGuid(), 3600)).IsValid.Should().BeTrue();
        new CheckAnswersValidator().Validate(new CheckAnswersCommand(Guid.NewGuid(),
            Enumerable.Range(0, 61).Select(_ => Guid.NewGuid()).ToList())).IsValid.Should().BeFalse();
        new CheckAnswersValidator().Validate(new CheckAnswersCommand(Guid.NewGuid(), [])).IsValid.Should().BeFalse();
    }
}
