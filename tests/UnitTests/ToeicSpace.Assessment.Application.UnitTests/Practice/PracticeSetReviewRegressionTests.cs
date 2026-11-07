using System.Text.Json;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;
using ToeicSpace.Assessment.Application.Practice.Common;
using ToeicSpace.Assessment.Application.Questions.Common;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetById;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetContent;

namespace ToeicSpace.Assessment.Application.UnitTests.Practice;

public class PracticeSetReviewRegressionTests
{
    [Theory]
    [InlineData(ContentStatus.Draft, false)]
    [InlineData(ContentStatus.Active, true)]
    [InlineData(ContentStatus.Archived, true)]
    [InlineData((ContentStatus)123, false)]
    public void StatusContract_AcceptsOnlyActiveAndArchived(ContentStatus status, bool valid)
    {
        new UpdatePracticeSetStatusValidator().Validate(new UpdatePracticeSetStatusCommand(Guid.NewGuid(), status)).IsValid.Should().Be(valid);
    }

    [Fact]
    public async Task LegacySibling_DoesNotBlockValidEntityEdit_ButStillBlocksPublish()
    {
        using var db = new TestDatabase();
        var set = db.AddPracticeSet(ToeicPart.Part5);
        var valid = db.AddQuestion(null, ToeicPart.Part5, null);
        var legacy = db.AddQuestion(null, ToeicPart.Part5, null);
        legacy.QuestionText = null;
        db.AddPracticeSetItems(set.Id, valid.Id, legacy.Id);
        await PublishedPracticeSetGuard.EnsurePublishedContentRemainsValidAsync(db.Context, [valid.Id], default);
        var publish = () => PracticeSetPublishingGuard.EnsureValidAsync(db.Context, set, [valid.Id, legacy.Id], default);
        (await publish.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.PracticeSetNotPublishable);
        valid.QuestionText = null;
        await db.Context.SaveChangesAsync();
        var invalidEdit = () => PublishedPracticeSetGuard.EnsurePublishedContentRemainsValidAsync(db.Context, [valid.Id], default);
        await invalidEdit.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task LegacySet_MetadataEditSucceeds_StalePreconditionDoesNotOverwrite()
    {
        using var db = new TestDatabase();
        var set = db.AddPracticeSet(ToeicPart.Part5);
        var legacy = db.AddQuestion(null, ToeicPart.Part5, null);
        legacy.QuestionText = null;
        db.AddPracticeSetItems(set.Id, legacy.Id);
        var handler = new UpdatePracticeSetHandler(db.Context, FakeCurrentUser.Admin(), db.CacheInvalidator);
        var command = new UpdatePracticeSetCommand(set.Id, set.Code, "Valid metadata edit", null,
            set.Kind, set.Part, null, null, 30, ExpectedUpdatedAt: set.CreatedAt);
        var result = await handler.Handle(command, default);
        result.Title.Should().Be("Valid metadata edit");
        var stale = () => handler.Handle(command with { Title = "Stale overwrite", ExpectedUpdatedAt = set.CreatedAt.AddDays(-1) }, default);
        (await stale.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.ConcurrencyConflict);
        set.Title.Should().Be("Valid metadata edit");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublicMetadata_OmitsOwner_ManagersRetainIt(bool manager)
    {
        using var db = new TestDatabase();
        var set = db.AddPracticeSet(ToeicPart.Part5);
        set.CreatedByUserId = Guid.NewGuid();
        await db.Context.SaveChangesAsync();
        var user = manager ? FakeCurrentUser.Admin() : new FakeCurrentUser();
        var detail = await new GetPracticeSetByIdHandler(db.Context, user).Handle(new(set.Id), default);
        detail.CreatedByUserId.Should().Be(manager ? set.CreatedByUserId : null);
        var json = JsonSerializer.Serialize(detail);
        json.Contains("CreatedByUserId").Should().Be(manager);
        var list = await new GetPracticeSetsHandler(db.Context, user).Handle(new(), default);
        list.Items.Single().CreatedByUserId.Should().Be(manager ? set.CreatedByUserId : null);
    }

    [Fact]
    public async Task Preview_PreservesAnswersAndQuestionStatus_ForAuthenticatedPolicy()
    {
        using var db = new TestDatabase();
        var set = db.AddPracticeSet(ToeicPart.Part5);
        var question = db.AddQuestion(null, ToeicPart.Part5, null);
        db.AddPracticeSetItems(set.Id, question.Id);
        var result = await new GetPracticeSetContentHandler(db.Context, FakeCurrentUser.Learner(), db.Cache)
            .Handle(new(set.Id), default);
        result.IncludesAnswers.Should().BeTrue();
        var content = result.Parts.Single().StandaloneQuestions.Single();
        content.CorrectAnswer.Should().Be(question.CorrectAnswer);
        content.Status.Should().Be(question.Status);
    }
}
