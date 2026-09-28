using FluentAssertions;
using ToeicSpace.Assessment.Domain.Entities;
using ToeicSpace.Assessment.Domain.Enums;
using ToeicSpace.Assessment.Domain.Rules;
using Xunit;

namespace ToeicSpace.Assessment.Domain.UnitTests;

public class ToeicPartRulesTests
{
    [Theory]
    [InlineData(ToeicPart.Part1, 1, 6)]
    [InlineData(ToeicPart.Part2, 7, 31)]
    [InlineData(ToeicPart.Part3, 32, 70)]
    [InlineData(ToeicPart.Part4, 71, 100)]
    [InlineData(ToeicPart.Part5, 101, 130)]
    [InlineData(ToeicPart.Part6, 131, 146)]
    [InlineData(ToeicPart.Part7, 147, 200)]
    public void GetStandardQuestionNumberRange_ShouldFollowEtsFormat(ToeicPart part, int first, int last)
    {
        // Act
        var range = ToeicPartRules.GetStandardQuestionNumberRange(part);

        // Assert
        range.Should().Be((first, last));
        ToeicPartRules.IsInStandardQuestionNumberRange(part, first).Should().BeTrue();
        ToeicPartRules.IsInStandardQuestionNumberRange(part, last).Should().BeTrue();
        ToeicPartRules.IsInStandardQuestionNumberRange(part, first - 1).Should().BeFalse();
        ToeicPartRules.IsInStandardQuestionNumberRange(part, last + 1).Should().BeFalse();
    }

    [Fact]
    public void Part2_ShouldOnlyAcceptAnswersAToC()
    {
        // Assert
        ToeicPartRules.GetOptionCount(ToeicPart.Part2).Should().Be(3);
        ToeicPartRules.IsValidAnswer(ToeicPart.Part2, AnswerOption.C).Should().BeTrue();
        ToeicPartRules.IsValidAnswer(ToeicPart.Part2, AnswerOption.D).Should().BeFalse();
        ToeicPartRules.IsValidAnswer(ToeicPart.Part5, AnswerOption.D).Should().BeTrue();
    }

    [Fact]
    public void IsValidAnswer_UndefinedValue_ShouldBeFalse()
    {
        // Assert
        ToeicPartRules.IsValidAnswer(ToeicPart.Part5, default).Should().BeFalse();
        ToeicPartRules.IsValidAnswer(ToeicPart.Part5, (AnswerOption)9).Should().BeFalse();
    }

    [Theory]
    [InlineData(ToeicPart.Part1, false)]
    [InlineData(ToeicPart.Part2, false)]
    [InlineData(ToeicPart.Part3, true)]
    [InlineData(ToeicPart.Part4, true)]
    [InlineData(ToeicPart.Part5, false)]
    [InlineData(ToeicPart.Part6, true)]
    [InlineData(ToeicPart.Part7, true)]
    public void RequiresPassage_ShouldMatchGroupedParts(ToeicPart part, bool expected)
    {
        // Assert
        ToeicPartRules.RequiresPassage(part).Should().Be(expected);
    }

    [Fact]
    public void Question_SettingPart_ShouldKeepSectionConsistent()
    {
        // Arrange
        var question = new ToeicQuestion { Part = ToeicPart.Part3 };

        // Act
        question.Part = ToeicPart.Part7;

        // Assert
        question.Section.Should().Be(ToeicSection.Reading);
    }
}
