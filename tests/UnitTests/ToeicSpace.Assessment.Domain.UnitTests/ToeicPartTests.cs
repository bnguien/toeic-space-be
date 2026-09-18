using FluentAssertions;
using ToeicSpace.Assessment.Domain.Enums;
using ToeicSpace.Assessment.Domain.Rules;
using Xunit;

namespace ToeicSpace.Assessment.Domain.UnitTests;

public class ToeicPartTests
{
    [Theory]
    [InlineData(ToeicPart.Part1)]
    [InlineData(ToeicPart.Part2)]
    [InlineData(ToeicPart.Part3)]
    [InlineData(ToeicPart.Part4)]
    public void ListeningParts_ShouldMapToListeningSection(ToeicPart part)
    {
        // Act
        var section = ToeicPartRules.GetSection(part);

        // Assert
        section.Should().Be(ToeicSection.Listening);
    }

    [Theory]
    [InlineData(ToeicPart.Part5)]
    [InlineData(ToeicPart.Part6)]
    [InlineData(ToeicPart.Part7)]
    public void ReadingParts_ShouldMapToReadingSection(ToeicPart part)
    {
        // Act
        var section = ToeicPartRules.GetSection(part);

        // Assert
        section.Should().Be(ToeicSection.Reading);
    }

    [Fact]
    public void StandardToeicTest_TotalQuestionsAcrossParts_ShouldEqual200()
    {
        // Act
        var listeningCount = Enum.GetValues<ToeicPart>()
            .Where(part => ToeicPartRules.GetSection(part) == ToeicSection.Listening)
            .Sum(ToeicPartRules.GetStandardQuestionCount);

        var readingCount = Enum.GetValues<ToeicPart>()
            .Where(part => ToeicPartRules.GetSection(part) == ToeicSection.Reading)
            .Sum(ToeicPartRules.GetStandardQuestionCount);

        // Assert
        listeningCount.Should().Be(ToeicPartRules.StandardSectionQuestionCount);
        readingCount.Should().Be(ToeicPartRules.StandardSectionQuestionCount);
        (listeningCount + readingCount).Should().Be(ToeicPartRules.StandardTestQuestionCount);
    }
}
