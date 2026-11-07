using ToeicSpace.Assessment.Application.Practice.Commands.DeletePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetById;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetContent;

namespace ToeicSpace.Assessment.Application.UnitTests.Practice;

public class PracticeSetAccessAndConflictTests
{
    [Theory]
    [InlineData("reorder")]
    [InlineData("add")]
    [InlineData("remove")]
    public async Task StaleQuestionList_CannotOverwriteAnotherEditorsSave(string change)
    {
        using var db = new TestDatabase();
        var teacher = FakeCurrentUser.ContentManager();
        var set = db.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        set.CreatedByUserId = teacher.UserId;
        var first = db.AddQuestion(null, ToeicPart.Part5, null);
        var second = db.AddQuestion(null, ToeicPart.Part5, null);
        var third = db.AddQuestion(null, ToeicPart.Part5, null);
        db.AddPracticeSetItems(set.Id, first.Id, second.Id);
        Guid[] baseline = [first.Id, second.Id];
        Guid[] desired = change switch
        {
            "reorder" => [second.Id, first.Id],
            "add" => [first.Id, second.Id, third.Id],
            _ => [second.Id]
        };
        var owner = new ReplacePracticeSetItemsHandler(db.Context, teacher, db.CacheInvalidator);
        await owner.Handle(new(set.Id, desired, baseline), default);
        var admin = new ReplacePracticeSetItemsHandler(db.Context, FakeCurrentUser.Admin(), db.CacheInvalidator);

        var stale = () => admin.Handle(new(set.Id, [third.Id, first.Id], baseline), default);

        (await stale.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.ConcurrencyConflict);
        (await db.Context.ToeicPracticeSetItems.OrderBy(item => item.OrderIndex).Select(item => item.QuestionId).ToListAsync())
            .Should().Equal(desired);
        // Explicit reload provides the new baseline and makes a later save valid.
        var fresh = await admin.Handle(new(set.Id, [third.Id, first.Id], desired), default);
        fresh.Items.Select(item => item.QuestionId).Should().Equal(third.Id, first.Id);
    }

    [Fact]
    public async Task MissingBaseline_IsRejectedInsteadOfFallingBackToLastWriteWins()
    {
        using var db = new TestDatabase();
        var set = db.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        var command = new ReplacePracticeSetItemsCommand(set.Id, [], null!);
        new ReplacePracticeSetItemsValidator().Validate(command).IsValid.Should().BeFalse();
        var handler = new ReplacePracticeSetItemsHandler(db.Context, FakeCurrentUser.Admin(), db.CacheInvalidator);
        var missing = () => handler.Handle(command, default);
        (await missing.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.ConcurrencyConflict);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RevokedPreview_IsDeniedEvenWhenTheServerCacheHeldContent(bool delete)
    {
        using var db = new TestDatabase();
        var set = db.AddPracticeSet(ToeicPart.Part5);
        var question = db.AddQuestion(null, ToeicPart.Part5, null);
        db.AddPracticeSetItems(set.Id, question.Id);
        var content = new GetPracticeSetContentHandler(db.Context, FakeCurrentUser.Learner(), db.Cache);
        (await content.Handle(new(set.Id), default)).QuestionCount.Should().Be(1);
        if (delete)
            await new DeletePracticeSetHandler(db.Context, FakeCurrentUser.Admin(), db.CacheInvalidator, db.Time)
                .Handle(new(set.Id), default);
        else
            await new UpdatePracticeSetStatusHandler(db.Context, FakeCurrentUser.Admin(), db.CacheInvalidator)
                .Handle(new(set.Id, ContentStatus.Archived), default);
        var unavailable = () => content.Handle(new(set.Id), default);
        (await unavailable.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.NotFound);
        var detail = new GetPracticeSetByIdHandler(db.Context, FakeCurrentUser.Learner());
        var hidden = () => detail.Handle(new(set.Id), default);
        (await hidden.Should().ThrowAsync<AppException>())
            .Which.Type.Should().Be(ErrorType.NotFound);
    }
}
