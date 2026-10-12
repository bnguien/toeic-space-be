using ToeicSpace.Assessment.Application.Questions.Queries.GetQuestionById;
using ToeicSpace.Assessment.Application.Questions.Queries.GetQuestions;

namespace ToeicSpace.Assessment.Application.UnitTests.Bank;

/// <summary>Browsing the question bank: the filters, search, paging and ordering of the admin CMS.</summary>
public class QuestionBrowsingTests
{
    private static GetQuestionsHandler Handler(TestDatabase database)
        => new(database.Context, new ContainsQuestionSearch());

    [Fact]
    public async Task Questions_AreFilteredByPart()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddQuestion(null, ToeicPart.Part2, null);

        var result = await Handler(database).Handle(new GetQuestionsQuery(Part: ToeicPart.Part5), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Should().OnlyContain(question => question.Part == ToeicPart.Part5);
    }

    [Fact]
    public async Task Questions_AreFilteredBySection()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part2, null);
        database.AddQuestion(null, ToeicPart.Part5, null);

        var result = await Handler(database).Handle(new GetQuestionsQuery(Section: ToeicSection.Reading), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.Part.Should().Be(ToeicPart.Part5);
    }

    [Fact]
    public async Task Questions_AreFilteredByDifficultyAndStatus()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part5, null, difficulty: QuestionDifficulty.Hard);
        database.AddQuestion(null, ToeicPart.Part5, null, difficulty: QuestionDifficulty.Easy);
        database.AddQuestion(null, ToeicPart.Part5, null, status: ContentStatus.Draft, difficulty: QuestionDifficulty.Hard);

        var hard = await Handler(database).Handle(new GetQuestionsQuery(DifficultyLevel: QuestionDifficulty.Hard), CancellationToken.None);
        var draft = await Handler(database).Handle(new GetQuestionsQuery(Status: ContentStatus.Draft), CancellationToken.None);

        hard.TotalCount.Should().Be(2);
        draft.Items.Should().ContainSingle().Which.Status.Should().Be(ContentStatus.Draft);
    }

    [Fact]
    public async Task BankOnly_SeparatesTestQuestionsFromBankQuestions()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        database.AddQuestion(null, ToeicPart.Part5, null);

        var bank = await Handler(database).Handle(new GetQuestionsQuery(BankOnly: true), CancellationToken.None);
        var inTests = await Handler(database).Handle(new GetQuestionsQuery(BankOnly: false), CancellationToken.None);

        bank.Items.Should().ContainSingle().Which.TestId.Should().BeNull();
        inTests.Items.Should().ContainSingle().Which.TestId.Should().Be(test.Id);
    }

    [Fact]
    public async Task TestQuestions_AreOrderedByQuestionNumber()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        database.AddQuestion(test.Id, ToeicPart.Part5, 103);
        database.AddQuestion(test.Id, ToeicPart.Part5, 101);
        database.AddQuestion(test.Id, ToeicPart.Part5, 102);

        var result = await Handler(database).Handle(new GetQuestionsQuery(TestId: test.Id), CancellationToken.None);

        result.Items.Select(question => question.QuestionNumber).Should().Equal(101, 102, 103);
    }

    [Fact]
    public async Task PracticeSetQuestions_KeepTheOrderOfTheSet()
    {
        using var database = new TestDatabase();
        var practiceSet = database.AddPracticeSet(ToeicPart.Part5);
        var first = database.AddQuestion(null, ToeicPart.Part5, null);
        var second = database.AddQuestion(null, ToeicPart.Part5, null);
        var unrelated = database.AddQuestion(null, ToeicPart.Part5, null);
        database.AddPracticeSetItems(practiceSet.Id, second.Id, first.Id);

        var result = await Handler(database).Handle(new GetQuestionsQuery(PracticeSetId: practiceSet.Id), CancellationToken.None);

        result.TotalCount.Should().Be(2);
        result.Items.Select(question => question.Id).Should().Equal(second.Id, first.Id);
        result.Items.Should().NotContain(question => question.Id == unrelated.Id);
    }

    [Fact]
    public async Task PassageFilter_ReturnsOnlyTheQuestionsOfThatPassage()
    {
        using var database = new TestDatabase();
        var passage = database.AddPassage(null, ToeicPart.Part7, "Text");
        var other = database.AddPassage(null, ToeicPart.Part7, "Other text");
        database.AddQuestion(null, ToeicPart.Part7, null, passage.Id);
        database.AddQuestion(null, ToeicPart.Part7, null, other.Id);

        var result = await Handler(database).Handle(new GetQuestionsQuery(PassageId: passage.Id), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.PassageId.Should().Be(passage.Id);
    }

    [Fact]
    public async Task Search_MatchesTheQuestionText()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part5, null, questionText: "The hiring committee approved the plan.");
        database.AddQuestion(null, ToeicPart.Part5, null, questionText: "Sales rose last quarter.");

        var result = await Handler(database).Handle(new GetQuestionsQuery(Search: " committee "), CancellationToken.None);

        result.Items.Should().ContainSingle().Which.QuestionText.Should().Contain("committee");
    }

    [Fact]
    public async Task Paging_SplitsTheResultsWithoutRepeating()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        for (var number = 101; number <= 105; number++)
        {
            database.AddQuestion(test.Id, ToeicPart.Part5, number);
        }

        var first = await Handler(database).Handle(new GetQuestionsQuery(Page: 1, PageSize: 2, TestId: test.Id), CancellationToken.None);
        var second = await Handler(database).Handle(new GetQuestionsQuery(Page: 2, PageSize: 2, TestId: test.Id), CancellationToken.None);
        var last = await Handler(database).Handle(new GetQuestionsQuery(Page: 3, PageSize: 2, TestId: test.Id), CancellationToken.None);

        first.TotalCount.Should().Be(5);
        first.TotalPages.Should().Be(3);
        first.HasNextPage.Should().BeTrue();
        first.Items.Should().HaveCount(2);
        last.Items.Should().ContainSingle();
        last.HasNextPage.Should().BeFalse();
        first.Items.Select(question => question.Id).Should().NotIntersectWith(second.Items.Select(question => question.Id));
    }

    [Fact]
    public async Task PageBeyondTheEnd_IsEmptyButKeepsTheTotal()
    {
        using var database = new TestDatabase();
        database.AddQuestion(null, ToeicPart.Part5, null);

        var result = await Handler(database).Handle(new GetQuestionsQuery(Page: 9), CancellationToken.None);

        result.Items.Should().BeEmpty();
        result.TotalCount.Should().Be(1);
    }

    [Fact]
    public async Task QuestionDetail_HasTheAnswerKeyAndExplanation()
    {
        using var database = new TestDatabase();
        var question = database.AddQuestion(null, ToeicPart.Part5, null);
        var handler = new GetQuestionByIdHandler(database.Context);

        var result = await handler.Handle(new GetQuestionByIdQuery(question.Id), CancellationToken.None);

        result.CorrectAnswer.Should().Be(AnswerOption.B);
        result.Explanation.Should().Be("Because B.");
    }

    [Fact]
    public async Task QuestionDetail_OfAnUnknownId_ShouldBeNotFound()
    {
        using var database = new TestDatabase();
        var handler = new GetQuestionByIdHandler(database.Context);

        var act = () => handler.Handle(new GetQuestionByIdQuery(Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.NotFound);
    }

    [Fact]
    public async Task DeletedQuestions_AreNotBrowsable()
    {
        using var database = new TestDatabase();
        var question = database.AddQuestion(null, ToeicPart.Part5, null);
        question.DeletedAt = database.Time.GetUtcNow().UtcDateTime;
        await database.Context.SaveChangesAsync();

        var list = await Handler(database).Handle(new GetQuestionsQuery(), CancellationToken.None);
        var act = () => new GetQuestionByIdHandler(database.Context).Handle(new GetQuestionByIdQuery(question.Id), CancellationToken.None);

        list.TotalCount.Should().Be(0);
        await act.Should().ThrowAsync<AppException>();
    }
}
