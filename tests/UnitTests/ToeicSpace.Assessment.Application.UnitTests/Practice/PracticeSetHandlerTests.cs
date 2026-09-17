using ToeicSpace.Assessment.Application.Common.Caching;
using ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetContent;

namespace ToeicSpace.Assessment.Application.UnitTests.Practice;

public class PracticeSetHandlerTests
{
    [Fact]
    public async Task ReplaceItems_WithQuestionOfAnotherPart_ShouldReject()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        var part1Question = database.AddQuestion(null, ToeicPart.Part1, null);
        var handler = new ReplacePracticeSetItemsHandler(database.Context, database.CacheInvalidator);

        var act = () => handler.Handle(new ReplacePracticeSetItemsCommand(practiceSet.Id, [part1Question.Id]), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.InvalidReference);
    }

    [Fact]
    public async Task ReplaceItems_ShouldKeepRequestedOrder()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        var first = database.AddQuestion(null, ToeicPart.Part5, null);
        var second = database.AddQuestion(null, ToeicPart.Part5, null);
        var handler = new ReplacePracticeSetItemsHandler(database.Context, database.CacheInvalidator);

        var result = await handler.Handle(new ReplacePracticeSetItemsCommand(practiceSet.Id, [second.Id, first.Id]), CancellationToken.None);

        result.Items.Select(item => item.QuestionId).Should().Equal(second.Id, first.Id);
    }

    [Fact]
    public async Task Content_ForLearner_ShouldIncludeAnswersAndSkipInactiveQuestions()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part3, ContentStatus.Active);
        var passage = database.AddPassage(null, ToeicPart.Part3);
        var active = database.AddQuestion(null, ToeicPart.Part3, null, passage.Id);
        var archived = database.AddQuestion(null, ToeicPart.Part3, null, passage.Id, ContentStatus.Archived);
        database.Context.ToeicPracticeSetItems.AddRange(
            new ToeicPracticeSetItem { PracticeSetId = practiceSet.Id, QuestionId = active.Id, OrderIndex = 0 },
            new ToeicPracticeSetItem { PracticeSetId = practiceSet.Id, QuestionId = archived.Id, OrderIndex = 1 });
        await database.Context.SaveChangesAsync();
        var handler = new GetPracticeSetContentHandler(database.Context, FakeCurrentUser.Learner(), database.Cache);

        var result = await handler.Handle(new GetPracticeSetContentQuery(practiceSet.Id), CancellationToken.None);

        result.QuestionCount.Should().Be(1);
        result.IncludesAnswers.Should().BeTrue();
        result.Parts.Single().Passages.Single().Questions.Single().CorrectAnswer.Should().Be(AnswerOption.B);
        database.Cache.Keys.Should().Contain(ContentCacheKeys.PracticeSet(practiceSet.Id, canManageContent: false));
    }
}
