using ToeicSpace.Assessment.Application.Features.PracticeSets.Commands.CreatePracticeSet;

namespace ToeicSpace.Assessment.Application.UnitTests.PracticeSets;

public class PracticeSetCreationTests
{
    private static CreatePracticeSetCommand Command(IReadOnlyList<Guid> ids, ContentStatus status = ContentStatus.Draft)
        => new("CREATE-WITH-QUESTIONS", "New practice", null, PracticeSetKind.Topic,
            ToeicPart.Part5, null, null, 30, QuestionIds: ids, Status: status);

    [Theory]
    [InlineData(ContentStatus.Draft)]
    [InlineData(ContentStatus.Active)]
    public async Task Create_SavesMetadataAndOrderedQuestionsTogether(ContentStatus status)
    {
        using var database = new TestDatabase();
        var first = database.AddQuestion(null, ToeicPart.Part5, null);
        var second = database.AddQuestion(null, ToeicPart.Part5, null);
        var user = FakeCurrentUser.ContentManager();
        var handler = new CreatePracticeSetHandler(database.Context, user);

        var result = await handler.Handle(Command([second.Id, first.Id], status), CancellationToken.None);

        result.Status.Should().Be(status);
        result.CreatedByUserId.Should().Be(user.UserId);
        result.Items.Select(item => item.QuestionId).Should().Equal(second.Id, first.Id);
        result.Items.Select(item => item.OrderIndex).Should().Equal(0, 1);
        database.Context.ToeicPracticeSets.Should().ContainSingle();
        database.Context.ToeicPracticeSetItems.Should().HaveCount(2);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("part")]
    [InlineData("inactive")]
    [InlineData("empty")]
    public async Task Create_InvalidSelectionDoesNotLeaveAnEmptyDraft(string failure)
    {
        using var database = new TestDatabase();
        var question = database.AddQuestion(null,
            failure == "part" ? ToeicPart.Part1 : ToeicPart.Part5, null,
            status: failure == "inactive" ? ContentStatus.Draft : ContentStatus.Active);
        Guid[] ids = failure switch
        {
            "missing" => [Guid.NewGuid()],
            "empty" => [],
            _ => [question.Id]
        };
        var handler = new CreatePracticeSetHandler(database.Context, FakeCurrentUser.ContentManager());

        var action = () => handler.Handle(Command(ids, ContentStatus.Active), CancellationToken.None);

        await action.Should().ThrowAsync<AppException>();
        database.Context.ToeicPracticeSets.Should().BeEmpty();
        database.Context.ToeicPracticeSetItems.Should().BeEmpty();
    }

    [Fact]
    public void Create_ValidatesSelectionLimitsDuplicatesAndStatus()
    {
        var validator = new CreatePracticeSetValidator();
        var id = Guid.NewGuid();
        validator.Validate(Command([id])).IsValid.Should().BeTrue();
        validator.Validate(Command([id, id])).IsValid.Should().BeFalse();
        validator.Validate(Command([Guid.Empty])).IsValid.Should().BeFalse();
        validator.Validate(Command(Enumerable.Range(0, 2001).Select(_ => Guid.NewGuid()).ToArray()))
            .IsValid.Should().BeFalse();
        validator.Validate(Command([], ContentStatus.Archived)).IsValid.Should().BeFalse();
    }
}
