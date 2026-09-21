using ToeicSpace.Assessment.Application.Passages.Commands.UpdatePassage;

namespace ToeicSpace.Assessment.Application.UnitTests.Passages;

/// <summary>A passage is the context its questions are asked about, so it follows the same locks.</summary>
public class PassageHandlerTests
{
    [Fact]
    public async Task Update_ContentOfPublishedTest_ShouldBeLocked()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        database.AddQuestion(test.Id, ToeicPart.Part3, 32, passage.Id);
        var handler = UpdateHandler(database);

        var act = () => handler.Handle(UpdateFrom(passage) with { Content = "W: Rewritten." }, CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.QuestionLocked);
        database.Context.ToeicPassages.Single().Content.Should().Be("W: Hello. M: Hi.");
    }

    [Fact]
    public async Task Update_AudioOfPublishedTest_ShouldBeLocked()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var passage = database.AddPassage(test.Id, ToeicPart.Part4);
        database.AddQuestion(test.Id, ToeicPart.Part4, 71, passage.Id);
        var handler = UpdateHandler(database);

        var command = UpdateFrom(passage) with { AudioUrl = "https://cdn.example.com/other.mp3" };
        var act = () => handler.Handle(command, CancellationToken.None);

        await act.Should().ThrowAsync<AppException>();
    }

    [Fact]
    public async Task Update_WithoutChanges_ShouldBeAllowedOnAPublishedTest()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        database.AddQuestion(test.Id, ToeicPart.Part3, 32, passage.Id);
        var handler = UpdateHandler(database);

        var result = await handler.Handle(UpdateFrom(passage), CancellationToken.None);

        result.Content.Should().Be(passage.Content);
    }

    [Fact]
    public async Task Update_ContentOfAnsweredQuestions_ShouldBeLocked()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        var question = database.AddQuestion(test.Id, ToeicPart.Part3, 32, passage.Id);
        database.AddAttemptAnswer(test.Id, question);
        var handler = UpdateHandler(database);

        var act = () => handler.Handle(UpdateFrom(passage) with { Transcript = "Rewritten script." }, CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.QuestionLocked);
    }

    [Fact]
    public async Task Update_ContentOfDraftTest_ShouldBeApplied()
    {
        using var database = new TestDatabase();
        var test = database.AddTest();
        var passage = database.AddPassage(test.Id, ToeicPart.Part3);
        database.AddQuestion(test.Id, ToeicPart.Part3, 32, passage.Id);
        var handler = UpdateHandler(database);

        var result = await handler.Handle(UpdateFrom(passage) with { Content = "W: Rewritten." }, CancellationToken.None);

        result.Content.Should().Be("W: Rewritten.");
    }

    [Fact]
    public async Task Update_MovingAPassageIntoAPublishedTest_ShouldBeLocked()
    {
        using var database = new TestDatabase();
        var published = database.AddTest(ContentStatus.Active, "ETS-2026-TEST-02");
        var passage = database.AddPassage(null, ToeicPart.Part7, "Bank text");
        var handler = UpdateHandler(database);

        var act = () => handler.Handle(UpdateFrom(passage) with { TestId = published.Id }, CancellationToken.None);

        (await act.Should().ThrowAsync<AppException>())
            .Which.Code.Should().Be(ErrorCodes.QuestionLocked);
    }

    private static UpdatePassageHandler UpdateHandler(TestDatabase database)
        => new(database.Context, database.CacheInvalidator);

    private static UpdatePassageCommand UpdateFrom(ToeicPassage passage)
        => new(
            passage.Id,
            passage.TestId,
            passage.Part,
            passage.PassageType,
            passage.Title,
            passage.Content,
            passage.AudioUrl,
            passage.ImageUrl,
            passage.Transcript,
            passage.OrderIndex);
}
