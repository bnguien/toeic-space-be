using System.Text.Json;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartTestAttempt;

namespace ToeicSpace.Assessment.Application.UnitTests.Attempts;

public sealed class StartTestAttemptTests
{
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task FullTest_CoversEveryPart_WithAServerDeadline()
    {
        using var h = new AttemptTestHarness();
        var test = h.AddPublishedTest();

        var attempt = await h.StartTest(test.Id, AttemptMode.FullTest, parts: [5]);

        attempt.Mode.Should().Be(AttemptMode.FullTest);
        attempt.Status.Should().Be(AttemptStatus.InProgress);
        attempt.Parts.Should().Equal(1, 2, 3, 4, 5, 6, 7);
        attempt.Questions.Should().HaveCount(200);
        attempt.Questions.Select(question => question.QuestionNumber).Should().BeInAscendingOrder();
        attempt.TimeLimitSeconds.Should().Be(120 * 60);
        attempt.Deadline.Should().Be(h.Now.AddMinutes(120));
        attempt.ServerNow.Should().Be(h.Now);
        attempt.Source.Kind.Should().Be(AttemptSourceKind.Test);
        attempt.Source.Category.Should().Be("ETS 2026");
        attempt.TestId.Should().Be(test.Id);
        attempt.Title.Should().Be(test.Title);
        attempt.Reveal.Questions.Should().BeEmpty();
    }

    [Fact]
    public async Task InProgressDocument_NeverLeaksAnswersScriptsOrTranslations()
    {
        using var h = new AttemptTestHarness();
        var test = h.AddPublishedTest();
        var attempt = await h.StartTest(test.Id, AttemptMode.FullTest);
        await h.Save(attempt.Id, attempt.Questions[0].Id, AnswerOption.A);

        var reloaded = await h.Get(attempt.Id);
        var json = JsonSerializer.Serialize(reloaded, Web);

        json.Should().NotContain("Because B.", "explanations stay private");
        json.Should().NotContain("Transcript", "question transcripts stay private");
        json.Should().NotContain("Bản dịch", "translations stay private");
        json.Should().NotContain("correctAnswer");
        json.Should().NotContain("W: Hello", "the Part 3/4 passage is the script");
        reloaded.Passages.Where(passage => passage.Part is 3 or 4).Should().OnlyContain(passage => passage.Content == null);
        reloaded.Passages.Where(passage => passage.Part is 6 or 7).Should().OnlyContain(passage => passage.Content == "<p>English text</p>");
        reloaded.Questions.Where(question => question.Part is 1 or 2).Should().OnlyContain(question => question.QuestionText == null);
        reloaded.Questions[0].UserAnswer.Should().Be(AnswerOption.A);
    }

    [Fact]
    public async Task Practice_SnapshotsOnlyTheChosenParts_WithoutATimer()
    {
        using var h = new AttemptTestHarness();
        var test = h.AddPublishedTest();

        var attempt = await h.StartTest(test.Id, AttemptMode.Practice, parts: [5, 3]);

        attempt.Parts.Should().Equal(3, 5);
        attempt.Questions.Should().HaveCount(39 + 30).And.OnlyContain(question => question.Part == 3 || question.Part == 5);
        attempt.Deadline.Should().BeNull();
        attempt.TimeLimitSeconds.Should().BeNull();
        attempt.Title.Should().Be($"{test.Title} · Part 3, 5");
        attempt.Passages.Should().ContainSingle().Which.Part.Should().Be(3);
    }

    [Fact]
    public async Task Start_PreservesMediaAndServesRetainedCopies()
    {
        using var h = new AttemptTestHarness();
        var test = h.AddPublishedTest();

        var attempt = await h.StartTest(test.Id, AttemptMode.Practice, parts: [1, 3]);

        h.Media.Preserved.Should().Contain(["https://cdn.example.com/photo.jpg", "https://cdn.example.com/audio.mp3"]);
        attempt.Questions.First(question => question.Part == 1).ImageUrl
            .Should().Be(FakeAttemptMediaStore.Retained("https://cdn.example.com/photo.jpg"));
        attempt.Passages.Single().AudioUrl.Should().Be(FakeAttemptMediaStore.Retained("https://cdn.example.com/audio.mp3"));
    }

    [Fact]
    public async Task StartAgain_ResumesTheInProgressAttempt_AndRefreshesActivity()
    {
        using var h = new AttemptTestHarness();
        var test = h.AddPublishedTest();
        var first = await h.StartTest(test.Id, AttemptMode.Practice, parts: [5]);
        h.Advance(TimeSpan.FromMinutes(10));

        var second = await h.StartTest(test.Id, AttemptMode.FullTest);

        second.Id.Should().Be(first.Id);
        second.Mode.Should().Be(AttemptMode.Practice, "resuming keeps the original attempt");
        second.LastActivityAt.Should().Be(h.Now);
    }

    [Fact]
    public async Task Restart_AbandonsTheInProgressAttempt_AndStartsANewOne()
    {
        using var h = new AttemptTestHarness();
        var test = h.AddPublishedTest();
        var first = await h.StartTest(test.Id, AttemptMode.Practice, parts: [5]);

        var second = await h.StartTest(test.Id, AttemptMode.FullTest, restart: true);

        second.Id.Should().NotBe(first.Id);
        second.Mode.Should().Be(AttemptMode.FullTest);
        (await h.Db.Context.ToeicAttempts.SingleAsync(attempt => attempt.Id == first.Id)).Status.Should().Be(AttemptStatus.Abandoned);
    }

    [Fact]
    public async Task UnpublishedTest_IsNotFound()
    {
        using var h = new AttemptTestHarness();
        var test = h.Db.AddTest(ContentStatus.Draft);
        h.Db.AddStandardQuestions(test.Id);

        var start = () => h.StartTest(test.Id, AttemptMode.FullTest);

        await start.Should().ThrowAsync<AppException>().Where(error => error.Type == ErrorType.NotFound);
    }

    [Fact]
    public async Task ContentManagers_CannotStartAttempts()
    {
        using var h = new AttemptTestHarness { User = FakeCurrentUser.ContentManager() };
        var test = h.AddPublishedTest();

        var start = () => h.StartTest(test.Id, AttemptMode.FullTest);

        await start.Should().ThrowAsync<AppException>().Where(error => error.Type == ErrorType.Forbidden);
    }

    [Theory]
    [InlineData(AttemptMode.Practice, null, false)]
    [InlineData(AttemptMode.Practice, new int[0], false)]
    [InlineData(AttemptMode.Practice, new[] { 5, 5 }, false)]
    [InlineData(AttemptMode.Practice, new[] { 8 }, false)]
    [InlineData(AttemptMode.PartPractice, new[] { 5 }, false)]
    [InlineData(AttemptMode.Practice, new[] { 1, 7 }, true)]
    [InlineData(AttemptMode.FullTest, null, true)]
    public void Validator_RequiresDistinctPartsForPractice(AttemptMode mode, int[]? parts, bool valid)
        => new StartTestAttemptValidator().Validate(new StartTestAttemptCommand(Guid.NewGuid(), mode, parts))
            .IsValid.Should().Be(valid);
}
