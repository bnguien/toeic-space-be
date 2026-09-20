using ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.DeleteQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestionStatus;
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
        database.AddAttemptAnswer(test.Id, question);
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
        database.AddAttemptAnswer(test.Id, question);
        var handler = UpdateHandler(database);

        var command = UpdateFrom(question, question.Version) with { Explanation = "Better explanation." };
        var result = await handler.Handle(command, CancellationToken.None);

        result.Version.Should().Be(2);
        result.Explanation.Should().Be("Better explanation.");
        database.Events.Events.Should().ContainSingle()
            .Which.Should().BeOfType<QuestionUpdatedIntegrationEvent>();
    }

    [Fact]
    public async Task Delete_QuestionOfPublishedPracticeSet_ShouldBeBlocked()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Active, code: "p5-lv1");
        var question = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(practiceSet.Id, question.Id);
        var handler = DeleteHandler(database);

        var act = () => handler.Handle(new DeleteQuestionCommand(question.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.QuestionLocked);
        (await act.Should().ThrowAsync<AppException>())
            .Which.Message.Should().Contain("p5-lv1");
        database.Context.ToeicPracticeSetItems.Should().ContainSingle();
    }

    [Fact]
    public async Task Delete_QuestionOfDraftPracticeSet_ShouldRemoveTheItem()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        var question = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(practiceSet.Id, question.Id);
        var handler = DeleteHandler(database);

        await handler.Handle(new DeleteQuestionCommand(question.Id), CancellationToken.None);

        database.Context.ToeicPracticeSetItems.Should().BeEmpty();
        database.Context.ToeicQuestions.IgnoreQueryFilters().Single().DeletedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task Archive_QuestionOfPublishedPracticeSet_ShouldBeBlocked()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Active, code: "p5-lv2");
        var question = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(practiceSet.Id, question.Id);
        var handler = StatusHandler(database);

        var act = () => handler.Handle(new UpdateQuestionStatusCommand(question.Id, ContentStatus.Archived), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.QuestionLocked);
        database.Context.ToeicQuestions.Single().Status.Should().Be(ContentStatus.Active);
    }

    [Fact]
    public async Task Publish_QuestionOfPublishedPracticeSet_ShouldBeAllowed()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Active);
        var question = database.AddQuestion(null, ToeicPart.Part5, null, status: ContentStatus.Draft);
        database.AddPracticeSetItems(practiceSet.Id, question.Id);
        var handler = StatusHandler(database);

        var result = await handler.Handle(new UpdateQuestionStatusCommand(question.Id, ContentStatus.Active), CancellationToken.None);

        result.Status.Should().Be(ContentStatus.Active);
    }

    private static DeleteQuestionHandler DeleteHandler(TestDatabase database)
        => new(
            database.Context,
            database.Events,
            database.CacheInvalidator,
            database.Time,
            TestDatabase.Logger<DeleteQuestionHandler>());

    private static UpdateQuestionStatusHandler StatusHandler(TestDatabase database)
        => new(
            database.Context,
            database.Events,
            database.CacheInvalidator,
            database.Time);

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
}
