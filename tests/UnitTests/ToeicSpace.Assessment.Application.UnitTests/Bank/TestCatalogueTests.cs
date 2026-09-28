using ToeicSpace.Assessment.Application.Tests.Queries.GetTestById;
using ToeicSpace.Assessment.Application.Tests.Queries.GetTestCategories;
using ToeicSpace.Assessment.Application.Tests.Queries.GetTests;

namespace ToeicSpace.Assessment.Application.UnitTests.Bank;

/// <summary>The exam list: public catalogue for learners, everything for content managers.</summary>
public class TestCatalogueTests
{
    [Fact]
    public async Task Learners_OnlySeePublishedTests()
    {
        using var database = new TestDatabase();
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-01");
        database.AddTest(ContentStatus.Draft, "ETS-2026-TEST-02");
        database.AddTest(ContentStatus.Archived, "ETS-2026-TEST-03");

        var learner = await new GetTestsHandler(database.Context, FakeCurrentUser.Learner())
            .Handle(new GetTestsQuery(), CancellationToken.None);
        var manager = await new GetTestsHandler(database.Context, FakeCurrentUser.ContentManager())
            .Handle(new GetTestsQuery(), CancellationToken.None);

        learner.Items.Should().ContainSingle().Which.Code.Should().Be("ETS-2026-TEST-01");
        manager.TotalCount.Should().Be(3);
    }

    [Fact]
    public async Task DraftTest_IsHiddenFromLearnersEvenByItsId()
    {
        using var database = new TestDatabase();
        var draft = database.AddTest(ContentStatus.Draft);

        var act = () => new GetTestByIdHandler(database.Context, FakeCurrentUser.Learner())
            .Handle(new GetTestByIdQuery(draft.Id), CancellationToken.None);
        var manager = await new GetTestByIdHandler(database.Context, FakeCurrentUser.ContentManager())
            .Handle(new GetTestByIdQuery(draft.Id), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>()).Which.Code.Should().Be(ErrorCodes.NotFound);
        manager.Status.Should().Be(ContentStatus.Draft);
    }

    [Fact]
    public async Task StatusFilter_IsIgnoredForLearners()
    {
        using var database = new TestDatabase();
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-01");
        database.AddTest(ContentStatus.Draft, "ETS-2026-TEST-02");

        var learner = await new GetTestsHandler(database.Context, FakeCurrentUser.Learner())
            .Handle(new GetTestsQuery(Status: ContentStatus.Draft), CancellationToken.None);

        learner.Items.Should().ContainSingle().Which.Status.Should().Be(ContentStatus.Active);
    }

    [Fact]
    public async Task Tests_AreSearchableByCodeTitleAndCategory()
    {
        using var database = new TestDatabase();
        database.AddTest(ContentStatus.Active, "CRACK-TOEIC-VOL1-TEST-04");
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-01");
        var handler = new GetTestsHandler(database.Context, FakeCurrentUser.ContentManager());

        var byCode = await handler.Handle(new GetTestsQuery(Search: "CRACK"), CancellationToken.None);
        var byCategory = await handler.Handle(new GetTestsQuery(Search: "ETS 2026"), CancellationToken.None);

        byCode.Items.Should().ContainSingle().Which.Code.Should().Contain("CRACK");
        byCategory.TotalCount.Should().Be(2, "AddTest always uses the ETS 2026 category");
    }

    [Fact]
    public async Task Tests_ArePagedAndOrderedByCategoryThenCode()
    {
        using var database = new TestDatabase();
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-03");
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-01");
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-02");
        var handler = new GetTestsHandler(database.Context, FakeCurrentUser.ContentManager());

        var page = await handler.Handle(new GetTestsQuery(Page: 1, PageSize: 2), CancellationToken.None);

        page.Items.Select(test => test.Code).Should().Equal("ETS-2026-TEST-01", "ETS-2026-TEST-02");
        page.TotalCount.Should().Be(3);
        page.HasNextPage.Should().BeTrue();
    }

    [Fact]
    public async Task TestDetail_CountsQuestionsPerPart()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        database.AddStandardQuestions(test.Id);

        var result = await new GetTestByIdHandler(database.Context, FakeCurrentUser.ContentManager())
            .Handle(new GetTestByIdQuery(test.Id), CancellationToken.None);

        result.QuestionCount.Should().Be(200);
        result.ListeningQuestionCount.Should().Be(100);
        result.ReadingQuestionCount.Should().Be(100);
        result.Parts.Single(part => part.Part == ToeicPart.Part7).QuestionCount.Should().Be(54);
    }

    [Fact]
    public async Task Categories_CountOnlyWhatTheAudienceMaySee()
    {
        using var database = new TestDatabase();
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-01");
        database.AddTest(ContentStatus.Draft, "ETS-2026-TEST-02");

        var learner = await new GetTestCategoriesHandler(database.Context, FakeCurrentUser.Learner())
            .Handle(new GetTestCategoriesQuery(), CancellationToken.None);
        var manager = await new GetTestCategoriesHandler(database.Context, FakeCurrentUser.ContentManager())
            .Handle(new GetTestCategoriesQuery(), CancellationToken.None);

        learner.Single().TestCount.Should().Be(1);
        manager.Single().TestCount.Should().Be(2);
    }
}
