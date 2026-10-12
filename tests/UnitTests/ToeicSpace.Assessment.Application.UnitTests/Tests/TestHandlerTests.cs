using ToeicSpace.Assessment.Application.Tests.Commands.DeleteTest;
using ToeicSpace.Assessment.Application.Tests.Commands.UpdateTestStatus;
using ToeicSpace.Assessment.Application.Tests.Queries.GetFullTest;

namespace ToeicSpace.Assessment.Application.UnitTests.Tests;

public class TestHandlerTests
{
    [Fact]
    public async Task GetFullTest_LearnerAskingForAnswers_ShouldBeForbidden()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var handler = new GetFullTestHandler(database.Context, FakeCurrentUser.Learner(), database.Cache);

        var act = () => handler.Handle(new GetFullTestQuery(test.Id, IncludeAnswers: true), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.AnswerKeyForbidden);
    }

    [Fact]
    public async Task GetFullTest_Learner_ShouldHideAnswersAndListeningScripts()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var conversation = database.AddPassage(test.Id, ToeicPart.Part3, content: "M: Where is the meeting?");
        var article = database.AddPassage(test.Id, ToeicPart.Part7, content: "Notice: the office is closed.");
        database.AddQuestion(test.Id, ToeicPart.Part3, 32, conversation.Id);
        database.AddQuestion(test.Id, ToeicPart.Part7, 147, article.Id);
        database.AddQuestion(test.Id, ToeicPart.Part5, 101, status: ContentStatus.Draft);
        var handler = new GetFullTestHandler(database.Context, FakeCurrentUser.Learner(), database.Cache);

        var result = await handler.Handle(new GetFullTestQuery(test.Id), CancellationToken.None);

        result.IncludesAnswers.Should().BeFalse();
        result.Parts.Select(part => part.Part).Should().Equal(ToeicPart.Part3, ToeicPart.Part7);

        var questions = result.Parts.SelectMany(part => part.Passages).SelectMany(passage => passage.Questions).ToList();
        questions.Should().HaveCount(2);
        questions.Should().OnlyContain(question => question.CorrectAnswer == null && question.Explanation == null && question.Transcript == null);

        var passages = result.Parts.SelectMany(part => part.Passages).ToList();
        passages.Single(passage => passage.Part == ToeicPart.Part3).Content.Should().BeNull();
        passages.Single(passage => passage.Part == ToeicPart.Part7).Content.Should().Be("Notice: the office is closed.");
    }

    [Fact]
    public async Task GetFullTest_DraftTest_ShouldBeHiddenFromLearners()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Draft);
        var handler = new GetFullTestHandler(database.Context, FakeCurrentUser.Learner(), database.Cache);

        var act = () => handler.Handle(new GetFullTestQuery(test.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Type.Should().Be(ErrorType.NotFound);
    }

    [Fact]
    public async Task GetFullTest_QuestionWithPassageOfAnotherTest_ShouldStillBeReturned()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var foreignPassage = database.AddPassage(testId: null, ToeicPart.Part3);
        database.AddQuestion(test.Id, ToeicPart.Part3, 32, foreignPassage.Id);
        var handler = new GetFullTestHandler(database.Context, FakeCurrentUser.ContentManager(), database.Cache);

        var result = await handler.Handle(new GetFullTestQuery(test.Id, IncludeAnswers: true), CancellationToken.None);

        result.Parts.Single().QuestionCount.Should().Be(1);
        result.Parts.Single().Passages.Single().Questions.Single().CorrectAnswer.Should().Be(AnswerOption.B);
    }

    [Fact]
    public async Task Publish_IncompleteTest_ShouldListProblems()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        var handler = StatusHandler(database);

        var act = () => handler.Handle(new UpdateTestStatusCommand(test.Id, ContentStatus.Active), CancellationToken.None);

        var exception = (await act.Should().ThrowAsync<AppException>()).Which;
        exception.Code.Should().Be(ErrorCodes.TestNotPublishable);
        exception.Message.Should().Contain("1 questions instead of 200");
    }

    [Fact]
    public async Task Publish_StandardTest_ShouldActivate()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        database.AddStandardQuestions(test.Id);
        var handler = StatusHandler(database);

        var result = await handler.Handle(new UpdateTestStatusCommand(test.Id, ContentStatus.Active), CancellationToken.None);

        result.Status.Should().Be(ContentStatus.Active);
        result.QuestionCount.Should().Be(200);
        result.Parts.Should().OnlyContain(part => part.QuestionCount == part.ExpectedQuestionCount);
    }

    [Fact]
    public async Task Delete_ShouldKeepQuestionsUsedByPracticeSets()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        var reused = database.AddQuestion(test.Id, ToeicPart.Part3, 32, passage.Id);
        var notReused = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        var practiceSet = new ToeicPracticeSet { Id = Guid.NewGuid(), Code = "p3-lv1", Title = "Part 3", Kind = PracticeSetKind.Level, Part = ToeicPart.Part3, Level = 1 };
        practiceSet.Items.Add(new ToeicPracticeSetItem { QuestionId = reused.Id, OrderIndex = 0 });
        database.Context.ToeicPracticeSets.Add(practiceSet);
        await database.Context.SaveChangesAsync();
        var handler = new DeleteTestHandler(database.Context, database.Events, database.CacheInvalidator, database.Time, TestDatabase.Logger<DeleteTestHandler>());

        await handler.Handle(new DeleteTestCommand(test.Id), CancellationToken.None);

        var questions = await database.Context.ToeicQuestions.IgnoreQueryFilters().ToListAsync();
        questions.Single(question => question.Id == reused.Id).Should().Match<ToeicQuestion>(question =>
            question.TestId == null && question.QuestionNumber == null && question.DeletedAt == null);
        questions.Single(question => question.Id == notReused.Id).DeletedAt.Should().NotBeNull();
        (await database.Context.ToeicPassages.SingleAsync()).TestId.Should().BeNull();
        (await database.Context.ToeicTests.CountAsync()).Should().Be(0);
    }

    private static UpdateTestStatusHandler StatusHandler(TestDatabase database)
        => new(database.Context, database.StructureInspector, database.CacheInvalidator, TestDatabase.Logger<UpdateTestStatusHandler>());
}
