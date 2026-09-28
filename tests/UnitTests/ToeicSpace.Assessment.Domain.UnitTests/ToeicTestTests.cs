using FluentAssertions;
using ToeicSpace.Assessment.Domain.Entities;
using ToeicSpace.Assessment.Domain.Enums;
using Xunit;

namespace ToeicSpace.Assessment.Domain.UnitTests;

public class ToeicTestTests
{
    [Fact]
    public void NewToeicTest_ShouldHaveCorrectInitialState()
    {
        // Arrange & Act
        var test = new ToeicTest
        {
            Title = "ETS TOEIC 2024 Test 1",
            Code = "ETS-2024-T1",
            TotalQuestions = 200,
            DurationMinutes = 120,
            Status = ContentStatus.Draft,
            IsActive = true,
            Category = "ETS"
        };

        // Assert
        test.Title.Should().Be("ETS TOEIC 2024 Test 1");
        test.Code.Should().Be("ETS-2024-T1");
        test.TotalQuestions.Should().Be(200);
        test.DurationMinutes.Should().Be(120);
        test.Status.Should().Be(ContentStatus.Draft);
        test.IsActive.Should().BeTrue();
        test.Passages.Should().NotBeNull().And.BeEmpty();
        test.Questions.Should().NotBeNull().And.BeEmpty();
        test.Attempts.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void ToeicTest_ShouldAllowAddingPassagesAndQuestions()
    {
        // Arrange
        var test = new ToeicTest
        {
            Title = "Mini Test Part 7",
            Code = "MINI-P7-01",
            TotalQuestions = 4,
            DurationMinutes = 10
        };

        var passage = new ToeicPassage
        {
            Part = ToeicPart.Part7,
            Content = "Notice: Office closure on Monday.",
            Test = test
        };

        var question1 = new ToeicQuestion
        {
            Part = ToeicPart.Part7,
            QuestionNumber = 1,
            QuestionText = "Why will the office be closed?",
            OptionA = "Maintenance",
            OptionB = "Holiday",
            OptionC = "Renovation",
            OptionD = "Meeting",
            CorrectAnswer = AnswerOption.B,
            DifficultyLevel = QuestionDifficulty.Medium,
            Passage = passage,
            Test = test
        };

        // Act
        test.Passages.Add(passage);
        test.Questions.Add(question1);

        // Assert
        test.Passages.Should().HaveCount(1);
        test.Questions.Should().HaveCount(1);
        test.Questions.First().Passage.Should().Be(passage);
    }
}
