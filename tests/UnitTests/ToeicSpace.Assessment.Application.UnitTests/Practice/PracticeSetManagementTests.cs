using ToeicSpace.Assessment.Application.Practice.Commands.CreatePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.DeletePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetContent;

namespace ToeicSpace.Assessment.Application.UnitTests.Practice;

public class PracticeSetManagementTests
{
    [Fact]
    public async Task Content_RequiresAuthentication_AndRechecksVisibilityOnCacheHit()
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Active);
        var question = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(set.Id, question.Id);
        var anonymous = new GetPracticeSetContentHandler(database.Context, new FakeCurrentUser(), database.Cache);
        var unauthenticated = () => anonymous.Handle(new GetPracticeSetContentQuery(set.Id), CancellationToken.None);
        (await unauthenticated.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.Unauthenticated);
        var learner = new GetPracticeSetContentHandler(database.Context, FakeCurrentUser.Learner(), database.Cache);
        await learner.Handle(new GetPracticeSetContentQuery(set.Id), CancellationToken.None);
        // Simulate a stale cache entry without using the status handler's invalidator.
        set.Status = ContentStatus.Archived;
        await database.Context.SaveChangesAsync();
        var hidden = () => learner.Handle(new GetPracticeSetContentQuery(set.Id), CancellationToken.None);
        (await hidden.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.NotFound);
    }

    [Theory]
    [InlineData("metadata", false)]
    [InlineData("items", false)]
    [InlineData("status", false)]
    [InlineData("delete", false)]
    [InlineData("metadata", true)]
    [InlineData("items", true)]
    [InlineData("status", true)]
    [InlineData("delete", true)]
    public async Task Teacher_CannotMutateAnotherOwnerOrImportedSet(string operation, bool imported)
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        set.CreatedByUserId = imported ? null : Guid.NewGuid();
        await database.Context.SaveChangesAsync();
        var user = FakeCurrentUser.ContentManager();

        var action = () => MutateAsync(database, user, set, operation);

        (await action.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.Forbidden);
        set.DeletedAt.Should().BeNull();
        set.Title.Should().Be("Practice");
        set.Status.Should().Be(ContentStatus.Draft);
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("items")]
    [InlineData("status")]
    [InlineData("delete")]
    public async Task Owner_CanMutateOwnSet(string operation)
    {
        using var database = new TestDatabase();
        var user = FakeCurrentUser.ContentManager();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        set.CreatedByUserId = user.UserId;
        await database.Context.SaveChangesAsync();

        await MutateAsync(database, user, set, operation);

        set.CreatedByUserId.Should().Be(user.UserId);
    }

    [Theory]
    [InlineData("metadata", false)]
    [InlineData("items", false)]
    [InlineData("status", false)]
    [InlineData("delete", false)]
    [InlineData("metadata", true)]
    [InlineData("items", true)]
    [InlineData("status", true)]
    [InlineData("delete", true)]
    public async Task Admin_CanManageAllSetsWithoutTakingOwnership(string operation, bool imported)
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        Guid? originalOwner = imported ? null : Guid.NewGuid();
        set.CreatedByUserId = originalOwner;
        await database.Context.SaveChangesAsync();

        await MutateAsync(database, FakeCurrentUser.Admin(), set, operation);

        set.CreatedByUserId.Should().Be(originalOwner);
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("items")]
    [InlineData("status")]
    [InlineData("delete")]
    public async Task Learner_CannotMutateEvenWithMatchingOwnerId(string operation)
    {
        using var database = new TestDatabase();
        var user = FakeCurrentUser.Learner();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        set.CreatedByUserId = user.UserId;
        await database.Context.SaveChangesAsync();

        var action = () => MutateAsync(database, user, set, operation);

        (await action.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.Forbidden);
    }

    [Fact]
    public async Task Create_RecordsOwnerAndDraft_AndRejectsLearner()
    {
        using var database = new TestDatabase();
        var user = FakeCurrentUser.ContentManager();
        var command = new CreatePracticeSetCommand("PBL26-NEW", "New practice", null, PracticeSetKind.Level, ToeicPart.Part5, 2, null, 30);
        var result = await new CreatePracticeSetHandler(database.Context, user).Handle(command, CancellationToken.None);
        result.CreatedByUserId.Should().Be(user.UserId);
        result.Status.Should().Be(ContentStatus.Draft);
        var action = () => new CreatePracticeSetHandler(database.Context, FakeCurrentUser.Learner()).Handle(command, CancellationToken.None);
        (await action.Should().ThrowAsync<AppException>()).Which.Type.Should().Be(ErrorType.Forbidden);
    }

    [Theory]
    [InlineData("empty")]
    [InlineData("inactive")]
    [InlineData("part")]
    [InlineData("answer")]
    [InlineData("option")]
    [InlineData("text")]
    [InlineData("deleted")]
    [InlineData("level")]
    public async Task Publish_RejectsInvalidContentWithoutChangingStatus(string failure)
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        var q = database.AddQuestion(null, ToeicPart.Part5, null);
        if (failure != "empty") database.AddPracticeSetItems(set.Id, q.Id);
        switch (failure)
        {
            case "inactive": q.Status = ContentStatus.Draft; break;
            case "part": q.Part = ToeicPart.Part2; break;
            case "answer": q.CorrectAnswer = (AnswerOption)999; break;
            case "option": q.OptionD = ""; break;
            case "text": q.QuestionText = null; break;
            case "deleted": q.MarkAsDeleted(DateTime.UtcNow); break;
            case "level": set.Level = 1; break;
        }
        await database.Context.SaveChangesAsync();

        var action = () => StatusHandler(database).Handle(new UpdatePracticeSetStatusCommand(set.Id, ContentStatus.Active), CancellationToken.None);

        (await action.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.PracticeSetNotPublishable);
        set.Status.Should().Be(ContentStatus.Draft);
    }

    [Theory]
    [InlineData(ToeicPart.Part1)]
    [InlineData(ToeicPart.Part2)]
    [InlineData(ToeicPart.Part3)]
    [InlineData(ToeicPart.Part4)]
    public async Task Publish_ListeningRequiresAudio(ToeicPart part)
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(part, ContentStatus.Draft);
        var passage = part is ToeicPart.Part3 or ToeicPart.Part4 ? database.AddPassage(null, part) : null;
        if (passage is not null) passage.AudioUrl = null;
        var q = database.AddQuestion(null, part, null, passage?.Id);
        database.AddPracticeSetItems(set.Id, q.Id);
        await database.Context.SaveChangesAsync();
        var action = () => StatusHandler(database).Handle(new UpdatePracticeSetStatusCommand(set.Id, ContentStatus.Active), CancellationToken.None);
        (await action.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.PracticeSetNotPublishable);
        q.AudioUrl = "https://example.com/audio.mp3";
        await database.Context.SaveChangesAsync();
        (await StatusHandler(database).Handle(new UpdatePracticeSetStatusCommand(set.Id, ContentStatus.Active), CancellationToken.None)).Status.Should().Be(ContentStatus.Active);
    }

    [Fact]
    public async Task Publish_RejectsMissingOrEmptyReadingPassage()
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(ToeicPart.Part6, ContentStatus.Draft);
        var passage = database.AddPassage(null, ToeicPart.Part6, content: null);
        var q = database.AddQuestion(null, ToeicPart.Part6, null, passage.Id);
        database.AddPracticeSetItems(set.Id, q.Id);
        var action = () => StatusHandler(database).Handle(new UpdatePracticeSetStatusCommand(set.Id, ContentStatus.Active), CancellationToken.None);
        (await action.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.PracticeSetNotPublishable);
        passage.Content = "Read this text.";
        passage.MarkAsDeleted(DateTime.UtcNow);
        await database.Context.SaveChangesAsync();
        (await action.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.PracticeSetNotPublishable);
    }

    [Fact]
    public async Task Publish_Unpublish_EditAndRepublish_Revalidates()
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);
        var q = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(set.Id, q.Id);
        var handler = StatusHandler(database);
        foreach (var status in new[] { ContentStatus.Active, ContentStatus.Archived, ContentStatus.Draft, ContentStatus.Active })
        {
            (await handler.Handle(new UpdatePracticeSetStatusCommand(set.Id, status), CancellationToken.None)).Status.Should().Be(status);
        }
        q.OptionA = "";
        await database.Context.SaveChangesAsync();
        var action = () => handler.Handle(new UpdatePracticeSetStatusCommand(set.Id, ContentStatus.Active), CancellationToken.None);
        (await action.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.PracticeSetNotPublishable);
    }

    [Fact]
    public async Task ActiveSet_ReordersAddsRemoves_WithoutChangingQuestionsOrAttempts()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var q1 = database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        var q2 = database.AddQuestion(test.Id, ToeicPart.Part5, 102);
        var q3 = database.AddQuestion(test.Id, ToeicPart.Part5, 103);
        var attempt = database.AddAttemptAnswer(test.Id, q1);
        var before = System.Text.Json.JsonSerializer.Serialize(attempt.Answers.Select(a => new { a.Id, a.QuestionId, a.CorrectAnswer, a.UserAnswer }));
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Active);
        database.AddPracticeSetItems(set.Id, q1.Id, q2.Id);
        var handler = new ReplacePracticeSetItemsHandler(database.Context, FakeCurrentUser.Admin(), database.CacheInvalidator);
        var reordered = await handler.Handle(new ReplacePracticeSetItemsCommand(set.Id, [q2.Id, q1.Id, q3.Id], [q1.Id, q2.Id]), CancellationToken.None);
        reordered.Items.Select(item => item.QuestionId).Should().Equal(q2.Id, q1.Id, q3.Id);
        var reduced = await handler.Handle(new ReplacePracticeSetItemsCommand(set.Id, [q3.Id, q2.Id], [q2.Id, q1.Id, q3.Id]), CancellationToken.None);
        reduced.Items.Select(item => item.OrderIndex).Should().Equal(0, 1);
        await new UpdatePracticeSetHandler(database.Context, FakeCurrentUser.Admin(), database.CacheInvalidator)
            .Handle(Metadata(set), CancellationToken.None);
        set.Status.Should().Be(ContentStatus.Active);
        database.Context.ToeicQuestions.Count().Should().Be(3);
        q1.Version.Should().Be(1);
        q1.CorrectAnswer.Should().Be(AnswerOption.B);
        System.Text.Json.JsonSerializer.Serialize(attempt.Answers.Select(a => new { a.Id, a.QuestionId, a.CorrectAnswer, a.UserAnswer })).Should().Be(before);
    }

    [Fact]
    public async Task ActiveReplacement_InvalidAnswerPreservesExistingOrder()
    {
        using var database = new TestDatabase();
        var set = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Active);
        var first = database.AddQuestion(null, ToeicPart.Part5, null);
        var invalid = database.AddQuestion(null, ToeicPart.Part5, null);
        invalid.OptionD = "";
        database.AddPracticeSetItems(set.Id, first.Id);
        var action = () => new ReplacePracticeSetItemsHandler(database.Context, FakeCurrentUser.Admin(), database.CacheInvalidator)
            .Handle(new ReplacePracticeSetItemsCommand(set.Id, [invalid.Id], [first.Id]), CancellationToken.None);
        (await action.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.PracticeSetNotPublishable);
        database.Context.ToeicPracticeSetItems.Single().QuestionId.Should().Be(first.Id);
    }

    [Fact]
    public async Task OwnedList_SearchStatusPagination_LeavesImportedRecordsUntouched()
    {
        using var database = new TestDatabase();
        var user = FakeCurrentUser.ContentManager();
        for (var i = 0; i < 62; i++) database.AddPracticeSet(ToeicPart.Part5, code: $"IMPORTED-{i}");
        for (var i = 0; i < 3; i++)
        {
            var owned = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Archived, PracticeSetKind.Level, 2, code: $"OWNED-{i}", title: $"My lesson {i}");
            owned.CreatedByUserId = user.UserId;
        }
        await database.Context.SaveChangesAsync();
        var result = await new GetPracticeSetsHandler(database.Context, user).Handle(new GetPracticeSetsQuery(Page: 2, PageSize: 2, Part: ToeicPart.Part5, Kind: PracticeSetKind.Level, Level: 2, Status: ContentStatus.Archived, Search: "My lesson", OwnedOnly: true), CancellationToken.None);
        result.TotalCount.Should().Be(3);
        result.Items.Should().ContainSingle().Which.CreatedByUserId.Should().Be(user.UserId);
        database.Context.ToeicPracticeSets.Count(s => s.CreatedByUserId == null).Should().Be(62);
        var learner = await new GetPracticeSetsHandler(database.Context, FakeCurrentUser.Learner()).Handle(new GetPracticeSetsQuery(Status: ContentStatus.Archived), CancellationToken.None);
        learner.TotalCount.Should().Be(62);
    }

    [Theory]
    [InlineData(PracticeSetKind.Level, null, false)]
    [InlineData(PracticeSetKind.Level, 0, false)]
    [InlineData(PracticeSetKind.Level, 6, false)]
    [InlineData(PracticeSetKind.Level, 1, true)]
    [InlineData(PracticeSetKind.Level, 5, true)]
    [InlineData(PracticeSetKind.Topic, 1, false)]
    [InlineData(PracticeSetKind.PartDrill, 1, false)]
    [InlineData(PracticeSetKind.Topic, null, true)]
    [InlineData(PracticeSetKind.PartDrill, null, true)]
    public void Metadata_LevelOnlyAppliesToLevelKind(PracticeSetKind kind, int? level, bool valid)
    {
        var command = new CreatePracticeSetCommand("PBL26", "Practice", null, kind, ToeicPart.Part5, level, null, 30);
        new CreatePracticeSetValidator().Validate(command).IsValid.Should().Be(valid);
    }

    private static UpdatePracticeSetStatusHandler StatusHandler(TestDatabase database)
        => new(database.Context, FakeCurrentUser.Admin(), database.CacheInvalidator);

    private static UpdatePracticeSetCommand Metadata(ToeicPracticeSet set)
        => new(set.Id, set.Code, "Updated practice", set.Description, set.Kind, set.Part, set.Level, set.TargetScore, set.DurationMinutes, set.OrderIndex);

    private static async Task MutateAsync(TestDatabase database, FakeCurrentUser user, ToeicPracticeSet set, string operation)
    {
        switch (operation)
        {
            case "metadata": await new UpdatePracticeSetHandler(database.Context, user, database.CacheInvalidator).Handle(Metadata(set), CancellationToken.None); break;
            case "items": await new ReplacePracticeSetItemsHandler(database.Context, user, database.CacheInvalidator).Handle(new ReplacePracticeSetItemsCommand(set.Id, [], []), CancellationToken.None); break;
            case "status": await new UpdatePracticeSetStatusHandler(database.Context, user, database.CacheInvalidator).Handle(new UpdatePracticeSetStatusCommand(set.Id, ContentStatus.Archived), CancellationToken.None); break;
            case "delete": await new DeletePracticeSetHandler(database.Context, user, database.CacheInvalidator, database.Time).Handle(new DeletePracticeSetCommand(set.Id), CancellationToken.None); break;
        }
    }
}
