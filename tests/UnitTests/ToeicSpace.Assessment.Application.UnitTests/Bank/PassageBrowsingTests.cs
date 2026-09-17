using ToeicSpace.Assessment.Application.Passages.Queries.GetPassageById;
using ToeicSpace.Assessment.Application.Passages.Queries.GetPassages;

namespace ToeicSpace.Assessment.Application.UnitTests.Bank;

/// <summary>Passages are the shared context the preview shows above a Part 3/4/6/7 question.</summary>
public class PassageBrowsingTests
{
    [Fact]
    public async Task Passages_AreFilteredByPartAndTest()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        database.AddPassage(test.Id, ToeicPart.Part7, "Test text");
        database.AddPassage(null, ToeicPart.Part7, "Bank text");
        database.AddPassage(null, ToeicPart.Part3);
        var handler = new GetPassagesHandler(database.Context);

        var part7 = await handler.Handle(new GetPassagesQuery(Part: ToeicPart.Part7), CancellationToken.None);
        var ofTest = await handler.Handle(new GetPassagesQuery(TestId: test.Id), CancellationToken.None);
        var bankOnly = await handler.Handle(new GetPassagesQuery(BankOnly: true), CancellationToken.None);

        part7.TotalCount.Should().Be(2);
        ofTest.Items.Should().ContainSingle().Which.TestId.Should().Be(test.Id);
        bankOnly.Items.Should().OnlyContain(passage => passage.TestId == null);
    }

    [Fact]
    public async Task PassageDetail_ListsItsQuestionsInOrder()
    {
        using var database = new TestDatabase();
        var passage = database.AddPassage(null, ToeicPart.Part6, "Text with ____135____.");
        var second = database.AddQuestion(null, ToeicPart.Part6, 136, passage.Id);
        var first = database.AddQuestion(null, ToeicPart.Part6, 135, passage.Id);
        var handler = new GetPassageByIdHandler(database.Context);

        var result = await handler.Handle(new GetPassageByIdQuery(passage.Id), CancellationToken.None);

        result.Content.Should().Contain("____135____");
        result.Questions.Select(question => question.Id).Should().Equal(first.Id, second.Id);
    }

    [Fact]
    public async Task PassageDetail_SplitsTheVietnameseTranslation()
    {
        using var database = new TestDatabase();
        var passage = database.AddPassage(null, ToeicPart.Part7, "<p>Dear Mr. Krug,</p><translation_split>Kính gửi ông Krug,");
        database.AddQuestion(null, ToeicPart.Part7, 147, passage.Id);
        var handler = new GetPassageByIdHandler(database.Context);

        var result = await handler.Handle(new GetPassageByIdQuery(passage.Id), CancellationToken.None);

        result.Content.Should().Be("<p>Dear Mr. Krug,</p>");
        result.ContentTranslation.Should().Be("Kính gửi ông Krug,");
    }

    [Fact]
    public async Task PassageDetail_OfAnUnknownId_ShouldBeNotFound()
    {
        using var database = new TestDatabase();
        var handler = new GetPassageByIdHandler(database.Context);

        var act = () => handler.Handle(new GetPassageByIdQuery(Guid.NewGuid()), CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.NotFound);
    }
}
