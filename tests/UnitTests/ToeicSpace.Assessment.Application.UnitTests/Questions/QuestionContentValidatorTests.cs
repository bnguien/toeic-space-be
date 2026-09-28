using ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;

namespace ToeicSpace.Assessment.Application.UnitTests.Questions;

public class QuestionContentValidatorTests
{
    private readonly CreateQuestionValidator _validator = new();

    [Fact]
    public void ValidPart5TestQuestion_ShouldPass()
    {
        var result = _validator.Validate(Part5Question());

        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Part2_WithAnswerD_ShouldFail()
    {
        var command = Part5Question() with
        {
            Part = ToeicPart.Part2,
            QuestionNumber = 10,
            OptionD = null,
            CorrectAnswer = AnswerOption.D
        };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "CorrectAnswer");
    }

    [Fact]
    public void Part2_WithOptionD_ShouldFail()
    {
        var command = Part5Question() with
        {
            Part = ToeicPart.Part2,
            QuestionNumber = 10,
            CorrectAnswer = AnswerOption.A
        };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "OptionD");
    }

    [Fact]
    public void Part3_WithoutPassage_ShouldFail()
    {
        var command = Part5Question() with { Part = ToeicPart.Part3, QuestionNumber = 32 };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "PassageId");
    }

    [Fact]
    public void Part5_WithPassage_ShouldFail()
    {
        var command = Part5Question() with { PassageId = Guid.NewGuid() };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "PassageId");
    }

    [Fact]
    public void Part1_WithoutImage_ShouldFail()
    {
        var command = Part5Question() with { Part = ToeicPart.Part1, QuestionNumber = 1, ImageUrl = null };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "ImageUrl");
    }

    [Theory]
    [InlineData(100)]
    [InlineData(131)]
    public void TestQuestion_NumberOutsidePartRange_ShouldFail(int questionNumber)
    {
        var command = Part5Question() with { QuestionNumber = questionNumber };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "QuestionNumber");
    }

    [Fact]
    public void BankQuestion_WithQuestionNumber_ShouldFail()
    {
        var command = Part5Question() with { TestId = null, QuestionNumber = 101 };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "QuestionNumber");
    }

    [Fact]
    public void RelativeMediaUrl_ShouldFail()
    {
        var command = Part5Question() with { AudioUrl = "68-70.mp3" };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "AudioUrl");
    }

    [Fact]
    public void MultiPageImage_ShouldPass()
    {
        var command = Part5Question() with
        {
            ImageUrl = "https://cdn.example.com/p1.png<image_split>https://cdn.example.com/p2.png"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://cdn.example.com/p1.png<image_split>javascript:alert(1)")]
    [InlineData("https://cdn.example.com/p1.png<image_split>")]
    [InlineData("<image_split>https://cdn.example.com/p1.png")]
    [InlineData("https://cdn.example.com/p1.png<image_split>p2.png")]
    public void MultiPageImage_WithInvalidItem_ShouldFail(string imageUrl)
    {
        var command = Part5Question() with { ImageUrl = imageUrl };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "ImageUrl");
    }

    [Fact]
    public void MediaUrlWithSpaces_ShouldPass()
    {
        var command = Part5Question() with
        {
            AudioUrl = "https://cdn.example.com/2023/Test 09/98-100.mp3"
        };

        var result = _validator.Validate(command);

        result.IsValid.Should().BeTrue();
    }

    [Theory]
    [InlineData("https://cdn.example.com/a.mp3\"onerror=\"x")]
    [InlineData("https://cdn.example.com/<b>.mp3")]
    [InlineData("https://cdn.example.com/a.mp3\nhttps://cdn.example.com/b.mp3")]
    public void MediaUrlWithMarkup_ShouldFail(string audioUrl)
    {
        var command = Part5Question() with { AudioUrl = audioUrl };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "AudioUrl");
    }

    [Fact]
    public void MultipleAudioUrls_ShouldFail()
    {
        var command = Part5Question() with
        {
            AudioUrl = "https://cdn.example.com/a.mp3<image_split>https://cdn.example.com/b.mp3"
        };

        var result = _validator.Validate(command);

        result.Errors.Should().Contain(error => error.PropertyName == "AudioUrl");
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
