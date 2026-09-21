using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetById;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;

namespace ToeicSpace.Assessment.Application.UnitTests.Bank;

/// <summary>The practice sets of the bank (level, topic and part drill collections).</summary>
public class PracticeSetCatalogueTests
{
    [Fact]
    public async Task Learners_OnlySeePublishedSets()
    {
        using var database = new TestDatabase();
        database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Active, code: "p5-lv1");
        database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft, code: "p5-lv2");

        var learner = await new GetPracticeSetsHandler(database.Context, FakeCurrentUser.Learner())
            .Handle(new GetPracticeSetsQuery(), CancellationToken.None);
        var manager = await new GetPracticeSetsHandler(database.Context, FakeCurrentUser.ContentManager())
            .Handle(new GetPracticeSetsQuery(), CancellationToken.None);

        learner.Items.Should().ContainSingle().Which.Code.Should().Be("p5-lv1");
        manager.TotalCount.Should().Be(2);
    }

    [Fact]
    public async Task Sets_AreFilteredByPartKindAndLevel()
    {
        using var database = new TestDatabase();
        database.AddPracticeSet(ToeicPart.Part5, kind: PracticeSetKind.Level, level: 1, code: "p5-lv1");
        database.AddPracticeSet(ToeicPart.Part5, kind: PracticeSetKind.PartDrill, code: "p5-economy-200");
        database.AddPracticeSet(ToeicPart.Part6, kind: PracticeSetKind.Level, level: 1, code: "p6-lv1");
        var handler = new GetPracticeSetsHandler(database.Context, FakeCurrentUser.ContentManager());

        var part5 = await handler.Handle(new GetPracticeSetsQuery(Part: ToeicPart.Part5), CancellationToken.None);
        var levels = await handler.Handle(new GetPracticeSetsQuery(Kind: PracticeSetKind.Level), CancellationToken.None);
        var level1Part6 = await handler.Handle(new GetPracticeSetsQuery(Part: ToeicPart.Part6, Level: 1), CancellationToken.None);

        part5.TotalCount.Should().Be(2);
        levels.TotalCount.Should().Be(2);
        level1Part6.Items.Should().ContainSingle().Which.Code.Should().Be("p6-lv1");
    }

    [Fact]
    public async Task Sets_AreSearchableByCodeAndTitle()
    {
        using var database = new TestDatabase();
        database.AddPracticeSet(ToeicPart.Part5, code: "p5-economy-200", title: "200 câu Economy");
        database.AddPracticeSet(ToeicPart.Part6, code: "p6-lv1", title: "Điền đoạn văn căn bản");
        var handler = new GetPracticeSetsHandler(database.Context, FakeCurrentUser.ContentManager());

        var byCode = await handler.Handle(new GetPracticeSetsQuery(Search: "economy"), CancellationToken.None);
        var byTitle = await handler.Handle(new GetPracticeSetsQuery(Search: "Điền đoạn"), CancellationToken.None);

        byCode.Items.Should().ContainSingle().Which.Code.Should().Be("p5-economy-200");
        byTitle.Items.Should().ContainSingle().Which.Code.Should().Be("p6-lv1");
    }

    [Fact]
    public async Task SetList_ReportsHowManyQuestionsEachSetHas()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5);
        var first = database.AddQuestion(null, ToeicPart.Part5, null);
        var second = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(practiceSet.Id, first.Id, second.Id);

        var result = await new GetPracticeSetsHandler(database.Context, FakeCurrentUser.ContentManager())
            .Handle(new GetPracticeSetsQuery(), CancellationToken.None);

        result.Items.Single().QuestionCount.Should().Be(2);
    }

    [Fact]
    public async Task SetDetail_KeepsTheLevelAndTargetScore()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5, kind: PracticeSetKind.Level, level: 3, code: "p5-lv3");

        var result = await new GetPracticeSetByIdHandler(database.Context, FakeCurrentUser.ContentManager())
            .Handle(new GetPracticeSetByIdQuery(practiceSet.Id), CancellationToken.None);

        result.Level.Should().Be(3);
        result.TargetScore.Should().Be(650);
    }

    [Fact]
    public async Task DraftSet_IsHiddenFromLearnersEvenByItsId()
    {
        using var database = new TestDatabase();
        var draft = database.AddPracticeSet(ToeicPart.Part5, ContentStatus.Draft);

        var act = () => new GetPracticeSetByIdHandler(database.Context, FakeCurrentUser.Learner())
            .Handle(new GetPracticeSetByIdQuery(draft.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.NotFound);
    }
}
