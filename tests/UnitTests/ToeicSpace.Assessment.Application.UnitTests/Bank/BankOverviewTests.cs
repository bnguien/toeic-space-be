using ToeicSpace.Assessment.Application.Bank.Queries.GetBankOverview;

namespace ToeicSpace.Assessment.Application.UnitTests.Bank;

/// <summary>The counts behind the overview page of the admin CMS.</summary>
public class BankOverviewTests
{
    [Fact]
    public async Task Overview_CountsQuestionsPerPartAndSection()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        database.AddStandardQuestions(test.Id);
        database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddQuestion(null, ToeicPart.Part5, null);

        var result = await new GetBankOverviewHandler(database.Context).Handle(new GetBankOverviewQuery(), CancellationToken.None);

        result.TotalQuestions.Should().Be(202);
        result.ListeningQuestions.Should().Be(100);
        result.ReadingQuestions.Should().Be(102);
        result.TestQuestions.Should().Be(200);
        result.BankQuestions.Should().Be(2);

        var part5 = result.Parts.Single(part => part.Part == ToeicPart.Part5);
        part5.Questions.Should().Be(32);
        part5.TestQuestions.Should().Be(30);
        part5.BankQuestions.Should().Be(2);
        part5.QuestionsPerTest.Should().Be(30);
        part5.Section.Should().Be(ToeicSection.Reading);
    }

    [Fact]
    public async Task Overview_ListsEveryPartEvenWhenEmpty()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part5, null);

        var result = await new GetBankOverviewHandler(database.Context).Handle(new GetBankOverviewQuery(), CancellationToken.None);

        result.Parts.Should().HaveCount(7);
        result.Parts.Single(part => part.Part == ToeicPart.Part1).Questions.Should().Be(0);
        result.Parts.Select(part => part.Part).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Overview_CountsStatusesAndDifficulties()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part5, null, difficulty: QuestionDifficulty.Hard);
        database.AddQuestion(null, ToeicPart.Part5, null, difficulty: QuestionDifficulty.Hard);
        database.AddQuestion(null, ToeicPart.Part5, null, status: ContentStatus.Draft, difficulty: QuestionDifficulty.Easy);

        var result = await new GetBankOverviewHandler(database.Context).Handle(new GetBankOverviewQuery(), CancellationToken.None);

        result.Statuses.Single(status => status.Status == ContentStatus.Active).Questions.Should().Be(2);
        result.Statuses.Single(status => status.Status == ContentStatus.Draft).Questions.Should().Be(1);
        result.Difficulties.Single(level => level.DifficultyLevel == QuestionDifficulty.Hard).Questions.Should().Be(2);
        result.Difficulties.Should().BeInAscendingOrder(level => level.DifficultyLevel);
    }

    [Fact]
    public async Task Overview_CountsPassagesTestsAndPracticeSets()
    {
        using var database = new TestDatabase();
        database.AddTest(ContentStatus.Active, "ETS-2026-TEST-01");
        database.AddTest(ContentStatus.Draft, "ETS-2026-TEST-02");
        database.AddPassage(null, ToeicPart.Part7, "Text");
        var levelSet = database.AddPracticeSet(ToeicPart.Part5, kind: PracticeSetKind.Level, level: 1, code: "p5-lv1");
        database.AddPracticeSet(ToeicPart.Part6, kind: PracticeSetKind.PartDrill, code: "p6-practice-100");
        var question = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(levelSet.Id, question.Id);

        var result = await new GetBankOverviewHandler(database.Context).Handle(new GetBankOverviewQuery(), CancellationToken.None);

        result.TotalTests.Should().Be(2);
        result.ActiveTests.Should().Be(1);
        result.TotalPassages.Should().Be(1);
        result.TotalPracticeSets.Should().Be(2);
        result.PracticeSets.Single(kind => kind.Kind == PracticeSetKind.Level).Sets.Should().Be(1);
        result.PracticeSets.Single(kind => kind.Kind == PracticeSetKind.Level).Questions.Should().Be(1);
        result.PracticeSets.Single(kind => kind.Kind == PracticeSetKind.PartDrill).Questions.Should().Be(0);
    }

    [Fact]
    public async Task Overview_IgnoresDeletedContent()
    {
        using var database = new TestDatabase();
        var kept = database.AddQuestion(null, ToeicPart.Part5, null);
        var deleted = database.AddQuestion(null, ToeicPart.Part5, null);
        deleted.DeletedAt = database.Time.GetUtcNow().UtcDateTime;
        await database.Context.SaveChangesAsync();

        var result = await new GetBankOverviewHandler(database.Context).Handle(new GetBankOverviewQuery(), CancellationToken.None);

        result.TotalQuestions.Should().Be(1);
        result.Parts.Single(part => part.Part == ToeicPart.Part5).Questions.Should().Be(1);
        kept.DeletedAt.Should().BeNull();
    }

    [Fact]
    public async Task Overview_OfAnEmptyBank_IsAllZeros()
    {
        using var database = new TestDatabase();

        var result = await new GetBankOverviewHandler(database.Context).Handle(new GetBankOverviewQuery(), CancellationToken.None);

        result.TotalQuestions.Should().Be(0);
        result.TotalTests.Should().Be(0);
        result.TotalPracticeSets.Should().Be(0);
        result.Parts.Should().HaveCount(7).And.OnlyContain(part => part.Questions == 0);
        result.Statuses.Should().BeEmpty();
        result.Difficulties.Should().BeEmpty();
    }
}
