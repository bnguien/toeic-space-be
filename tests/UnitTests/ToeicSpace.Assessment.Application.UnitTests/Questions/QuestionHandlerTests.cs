using ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestion;
using ToeicSpace.BuildingBlocks.Messaging.Events;

namespace ToeicSpace.Assessment.Application.UnitTests.Questions;

public class QuestionHandlerTests
{
    [Fact]
    public async Task Create_WithPassageFromAnotherTest_ShouldRejectReference()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var otherTest = database.AddTest(code: "ETS-2026-TEST-02");
        var passage = database.AddPassage(otherTest.Id, ToeicPart.Part3);
        var handler = CreateHandler(database);

        var act = () => handler.Handle(Part3Question(test.Id, passage.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.InvalidReference);
    }

    [Fact]
    public async Task Create_WithDuplicateQuestionNumber_ShouldConflict()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        database.AddQuestion(test.Id, ToeicPart.Part3, 32, passage.Id);
        var handler = CreateHandler(database);

        var act = () => handler.Handle(Part3Question(test.Id, passage.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.DuplicateQuestionNumber);
    }

    [Fact]
    public async Task Create_InPublishedTest_ShouldBeLocked()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        var handler = CreateHandler(database);

        var act = () => handler.Handle(Part3Question(test.Id, passage.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.QuestionLocked);
    }

    [Fact]
    public async Task Create_Valid_ShouldSaveQuestionAndPublishEvent()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        var handler = CreateHandler(database);

        var result = await handler.Handle(Part3Question(test.Id, passage.Id), CancellationToken.None);

        result.Section.Should().Be(ToeicSection.Listening);
        result.Version.Should().Be(1);
        (await database.Context.ToeicQuestions.CountAsync()).Should().Be(1);
        database.Events.Events.Should().ContainSingle()
            .Which.Should().BeOfType<QuestionCreatedIntegrationEvent>();
    }

    [Fact]
    public async Task Update_WithStaleVersion_ShouldConflict()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var question = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        question.IncreaseVersion();
        await database.Context.SaveChangesAsync();
        var handler = UpdateHandler(database);

        var act = () => handler.Handle(UpdateFrom(question, expectedVersion: 1), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.ConcurrencyConflict);
    }

    [Fact]
    public async Task Update_AnswerKeyOfAnsweredQuestion_ShouldBeLocked()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var question = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        AddAttemptAnswer(database, test, question);
        var handler = UpdateHandler(database);

        var command = UpdateFrom(question, question.Version) with { CorrectAnswer = AnswerOption.C };
        var act = () => handler.Handle(command, CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.QuestionLocked);
    }

    [Fact]
    public async Task Update_ExplanationOfAnsweredQuestion_ShouldIncreaseVersion()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var question = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        AddAttemptAnswer(database, test, question);
        var handler = UpdateHandler(database);

        var command = UpdateFrom(question, question.Version) with { Explanation = "Better explanation." };
        var result = await handler.Handle(command, CancellationToken.None);

        result.Version.Should().Be(2);
        result.Explanation.Should().Be("Better explanation.");
        database.Events.Events.Should().ContainSingle()
            .Which.Should().BeOfType<QuestionUpdatedIntegrationEvent>();
    }

    private static CreateQuestionHandler CreateHandler(TestDatabase database)
        => new(
            database.Context,
            database.ReferenceGuard,
            database.Events,
            database.CacheInvalidator,
            FakeCurrentUser.ContentManager(),
            database.Time,
            TestDatabase.Logger<CreateQuestionHandler>());

    private static UpdateQuestionHandler UpdateHandler(TestDatabase database)
        => new(
            database.Context,
            database.ReferenceGuard,
            database.Events,
            database.CacheInvalidator,
            database.Time,
            TestDatabase.Logger<UpdateQuestionHandler>());

    private static CreateQuestionCommand Part3Question(Guid testId, Guid passageId)
        => new(
            TestId: testId,
            PassageId: passageId,
            Part: ToeicPart.Part3,
            QuestionNumber: 32,
            QuestionText: "Where does the man work?",
            AudioUrl: null,
            ImageUrl: null,
            OptionA: "At a bank",
            OptionB: "At a hotel",
            OptionC: "At a library",
            OptionD: "At a gym",
            CorrectAnswer: AnswerOption.B,
            Explanation: null,
            Transcript: null);

    private static UpdateQuestionCommand UpdateFrom(ToeicQuestion question, int expectedVersion)
        => new(
            question.Id,
            expectedVersion,
            question.TestId,
            question.PassageId,
            question.Part,
            question.QuestionNumber,
            question.QuestionText,
            question.AudioUrl,
            question.ImageUrl,
            question.OptionA,
            question.OptionB,
            question.OptionC,
            question.OptionD,
            question.CorrectAnswer,
            question.Explanation,
            question.Transcript,
            question.DifficultyLevel,
            question.Topic,
            question.OrderIndex,
            question.PreferAiExplanation);

    private static void AddAttemptAnswer(TestDatabase database, ToeicTest test, ToeicQuestion question)
    {
        var attempt = new ToeicAttempt
        {
            Id = Guid.NewGuid(),
            UserId = Guid.NewGuid(),
            TestId = test.Id,
            StartTime = FixedTimeProvider.DefaultNow.UtcDateTime
        };

        attempt.Answers.Add(new ToeicAttemptAnswer
        {
            Id = Guid.NewGuid(),
            QuestionId = question.Id,
            UserAnswer = AnswerOption.A,
            CorrectAnswer = question.CorrectAnswer
        });

        database.Context.ToeicAttempts.Add(attempt);
        database.Context.SaveChanges();
    }
}
