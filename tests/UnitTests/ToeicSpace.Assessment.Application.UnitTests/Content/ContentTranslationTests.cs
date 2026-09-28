using ToeicSpace.Assessment.Application.Common.Content;
using ToeicSpace.Assessment.Application.Common.Validation;
using ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;
using ToeicSpace.Assessment.Application.Questions.Queries.GetQuestionById;
using ToeicSpace.Assessment.Application.Tests.Queries.GetFullTest;

namespace ToeicSpace.Assessment.Application.UnitTests.Content;

public class ContentTranslationTests
{
    private const string Part6Content =
        "<p>I have much to offer ____135____ as an employee.</p><translation_split>Tôi có nhiều điều đóng góp [đội ngũ].";

    private const string Part3Transcript =
        "<p><strong>W:</strong> The machine is broken.</p><hr/><br/><p><strong>W:</strong> Máy bị hỏng rồi.</p>";

    [Fact]
    public void PassageContent_IsSplitAtTheTranslationMarker()
    {
        TranslatedText.PassageText(Part6Content)
            .Should().Be("<p>I have much to offer ____135____ as an employee.</p>");
        TranslatedText.PassageTranslation(Part6Content)
            .Should().Be("Tôi có nhiều điều đóng góp [đội ngũ].");
    }

    [Fact]
    public void Transcript_IsSplitAtTheRuleAndLeadingBreaksAreDropped()
    {
        TranslatedText.TranscriptText(Part3Transcript)
            .Should().Be("<p><strong>W:</strong> The machine is broken.</p>");
        TranslatedText.TranscriptTranslation(Part3Transcript)
            .Should().Be("<p><strong>W:</strong> Máy bị hỏng rồi.</p>");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyValues_HaveNeitherTextNorTranslation(string? value)
    {
        TranslatedText.PassageText(value).Should().BeNull();
        TranslatedText.PassageTranslation(value).Should().BeNull();
        TranslatedText.TranscriptTranslation(value).Should().BeNull();
    }

    [Fact]
    public void ContentWithoutMarker_IsReturnedWhole()
    {
        TranslatedText.PassageText("  M: Hello.\nW: Hi.  ").Should().Be("M: Hello.\nW: Hi.");
        TranslatedText.PassageTranslation("M: Hello.").Should().BeNull();
        TranslatedText.TranscriptText("(A) In the front row.").Should().Be("(A) In the front row.");
    }

    [Fact]
    public async Task GetFullTest_Learner_NeverReceivesTranslations()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var text = database.AddPassage(test.Id, ToeicPart.Part6, content: Part6Content);
        database.AddQuestion(test.Id, ToeicPart.Part6, 135, text.Id);
        var handler = new GetFullTestHandler(database.Context, FakeCurrentUser.Learner(), database.Cache);

        var result = await handler.Handle(new GetFullTestQuery(test.Id), CancellationToken.None);

        var passage = result.Parts.Single().Passages.Single();
        passage.Content.Should().Be("<p>I have much to offer ____135____ as an employee.</p>");
        passage.ContentTranslation.Should().BeNull();
        passage.Questions.Single().TranscriptTranslation.Should().BeNull();
    }

    [Fact]
    public async Task GetFullTest_ManagerWithAnswers_ReceivesTextAndTranslationSeparately()
    {
        using var database = new TestDatabase();
        var test = database.AddTest(ContentStatus.Active);
        var conversation = database.AddPassage(test.Id, ToeicPart.Part3, content: "M: Hi.<translation_split>M: Chào.");
        var question = database.AddQuestion(test.Id, ToeicPart.Part3, 32, conversation.Id);
        question.Transcript = Part3Transcript;
        await database.Context.SaveChangesAsync();
        var handler = new GetFullTestHandler(database.Context, FakeCurrentUser.ContentManager(), database.Cache);

        var result = await handler.Handle(new GetFullTestQuery(test.Id, IncludeAnswers: true), CancellationToken.None);

        var passage = result.Parts.Single().Passages.Single();
        passage.Content.Should().Be("M: Hi.");
        passage.ContentTranslation.Should().Be("M: Chào.");
        passage.Questions.Single().Transcript.Should().Be("<p><strong>W:</strong> The machine is broken.</p>");
        passage.Questions.Single().TranscriptTranslation.Should().Be("<p><strong>W:</strong> Máy bị hỏng rồi.</p>");
    }

    [Fact]
    public async Task QuestionDetail_SplitsTheTranscript()
    {
        using var database = new TestDatabase();
        var question = database.AddQuestion(testId: null, ToeicPart.Part3, questionNumber: null);
        question.Transcript = Part3Transcript;
        await database.Context.SaveChangesAsync();

        var detail = await new GetQuestionByIdHandler(database.Context)
            .Handle(new GetQuestionByIdQuery(question.Id), CancellationToken.None);

        detail.Transcript.Should().Be("<p><strong>W:</strong> The machine is broken.</p>");
        detail.TranscriptTranslation.Should().Be("<p><strong>W:</strong> Máy bị hỏng rồi.</p>");
    }

    [Theory]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("<SCRIPT>alert(1)</SCRIPT>")]
    [InlineData("<a href = ' data:text/html,x'>x</a>")]
    [InlineData("<svg/onload=alert(1)>")]
    [InlineData("<iframe src=\"https://evil.example\"></iframe>")]
    public void UnsafeMarkup_IsRejected(string markup)
    {
        RichTextRules.BeSafe(markup).Should().BeFalse();

        var result = new CreateQuestionValidator().Validate(Part5Question() with { Explanation = markup });

        result.Errors.Should().Contain(error => error.PropertyName == "Explanation");
    }

    [Theory]
    [InlineData("Personal data: see page 2.")]
    [InlineData("javascript: a programming language")]
    [InlineData("Bonus = one + two")]
    [InlineData("<div class=\"tp-exp-rich-wrapper\"><p style=\"white-space: pre-line\">Đáp án <strong>B</strong></p></div>")]
    [InlineData("Gửi: Renata Alvarez <ralvarez@brookhillcity.gov>")]
    [InlineData("<a href=\"mailto:apply@mapleglen.edu\" rel=\"noopener noreferrer\" target=\"_blank\">apply@mapleglen.edu</a>")]
    public void ImportedContentStyles_AreAccepted(string markup)
    {
        RichTextRules.BeSafe(markup).Should().BeTrue();
    }

    private static CreateQuestionCommand Part5Question()
        => new(
            TestId: Guid.NewGuid(),
            PassageId: null,
            Part: ToeicPart.Part5,
            QuestionNumber: 101,
            QuestionText: "The report ____ yesterday.",
            AudioUrl: null,
            ImageUrl: null,
            OptionA: "submit",
            OptionB: "was submitted",
            OptionC: "submitting",
            OptionD: "submits",
            CorrectAnswer: AnswerOption.B,
            Explanation: null,
            Transcript: null);
}
