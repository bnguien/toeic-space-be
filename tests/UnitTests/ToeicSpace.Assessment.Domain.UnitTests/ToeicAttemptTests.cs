using FluentAssertions;
using ToeicSpace.Assessment.Domain.Entities;
using ToeicSpace.Assessment.Domain.Enums;
using Xunit;

namespace ToeicSpace.Assessment.Domain.UnitTests;

public class ToeicAttemptTests
{
    [Fact]
    public void NewAttempt_ShouldInitializeWithDefaultValues()
    {
        // Arrange & Act
        var attempt = new ToeicAttempt
        {
            UserId = Guid.NewGuid(),
            Status = AttemptStatus.InProgress,
            Mode = AttemptMode.FullTest,
            StartTime = DateTime.UtcNow
        };

        // Assert
        attempt.Status.Should().Be(AttemptStatus.InProgress);
        attempt.Mode.Should().Be(AttemptMode.FullTest);
        attempt.TotalScore.Should().Be(0);
        attempt.ListeningScore.Should().Be(0);
        attempt.ReadingScore.Should().Be(0);
        attempt.EndTime.Should().BeNull();
        attempt.Answers.Should().NotBeNull().And.BeEmpty();
    }

    [Fact]
    public void CompletedAttempt_ScoreCalculation_ShouldBeConsistent()
    {
        // Arrange
        var startTime = DateTime.UtcNow.AddMinutes(-90);
        var endTime = DateTime.UtcNow;

        var attempt = new ToeicAttempt
        {
            UserId = Guid.NewGuid(),
            Status = AttemptStatus.Completed,
            Mode = AttemptMode.FullTest,
            StartTime = startTime,
            EndTime = endTime,
            DurationSeconds = 5400,
            ListeningScore = 405,
            ReadingScore = 380,
            TotalScore = 405 + 380,
            TotalQuestions = 200,
            TotalCorrect = 160
        };

        // Act & Assert
        attempt.TotalScore.Should().Be(785);
        attempt.ListeningScore.Should().BeInRange(0, 495);
        attempt.ReadingScore.Should().BeInRange(0, 495);
        attempt.TotalScore.Should().BeInRange(0, 990);
        attempt.Status.Should().Be(AttemptStatus.Completed);
        attempt.DurationSeconds.Should().Be(5400);

        var accuracy = (double)attempt.TotalCorrect / attempt.TotalQuestions * 100;
        accuracy.Should().Be(80.0);
    }

    [Fact]
    public void ToeicAttemptAnswer_MatchingCorrectAnswer_ShouldBeCorrect()
    {
        // Arrange
        var question = new ToeicQuestion
        {
            Part = ToeicPart.Part5,
            QuestionNumber = 101,
            QuestionText = "Ms. Tanaka will ____ the annual report tomorrow.",
            OptionA = "present",
            OptionB = "presents",
            OptionC = "presented",
            OptionD = "presenting",
            CorrectAnswer = AnswerOption.A
        };

        // Act
        var answer = new ToeicAttemptAnswer
        {
            QuestionId = question.Id,
            UserAnswer = AnswerOption.A,
            CorrectAnswer = question.CorrectAnswer,
            IsCorrect = question.CorrectAnswer == AnswerOption.A,
            TimeSpentSeconds = 25
        };

        // Assert
        answer.IsCorrect.Should().BeTrue();
        answer.UserAnswer.Should().Be(AnswerOption.A);
        answer.CorrectAnswer.Should().Be(AnswerOption.A);
    }
}
