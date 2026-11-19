using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.UnitTests.Attempts;

public sealed class ToeicScoreConversionTests
{
    // Expected values were produced by StudyChill exam/services/grading.py scaled_score().
    [Theory]
    [InlineData(0, 100, 5)]
    [InlineData(5, 100, 5)]
    [InlineData(6, 100, 35)]
    [InlineData(10, 100, 55)]
    [InlineData(33, 100, 165)]
    [InlineData(50, 100, 250)]
    [InlineData(51, 100, 255)]
    [InlineData(73, 100, 365)]
    [InlineData(88, 100, 435)]
    [InlineData(95, 100, 470)]
    [InlineData(96, 100, 495)]
    [InlineData(100, 100, 495)]
    [InlineData(0, 0, 5)]
    [InlineData(1, 6, 85)]
    [InlineData(3, 6, 250)]
    [InlineData(99, 196, 250)]
    [InlineData(13, 25, 260)]
    [InlineData(17, 39, 220)]
    public void ScaledScore_MatchesTheStudyChillConversion(int correct, int total, int expected)
        => ToeicScoreConversion.ScaledScore(correct, total).Should().Be(expected);

    [Theory]
    [InlineData(990, "C2", "Thành thạo cao cấp")]
    [InlineData(905, "C2", "Thành thạo cao cấp")]
    [InlineData(904, "C1", "Vững vàng cao cấp")]
    [InlineData(785, "C1", "Vững vàng cao cấp")]
    [InlineData(784, "B2", "Trung cấp trên")]
    [InlineData(605, "B2", "Trung cấp trên")]
    [InlineData(405, "B1", "Trung cấp cơ bản")]
    [InlineData(255, "A2", "Sơ cấp")]
    [InlineData(254, "A1", "Cơ bản")]
    [InlineData(10, "A1", "Cơ bản")]
    public void Cefr_UsesTheStudyChillBands(int total, string level, string title)
        => ToeicScoreConversion.Cefr(total).Should().Be(new CefrBand(level, title));

    [Fact]
    public void CoversSection_RequiresEveryPartOfTheSection()
    {
        ToeicPart[] listening = [ToeicPart.Part1, ToeicPart.Part2, ToeicPart.Part3, ToeicPart.Part4];

        ToeicScoreConversion.CoversSection(listening, ToeicSection.Listening).Should().BeTrue();
        ToeicScoreConversion.CoversSection(listening, ToeicSection.Reading).Should().BeFalse();
        ToeicScoreConversion.CoversSection([ToeicPart.Part5, ToeicPart.Part6], ToeicSection.Reading).Should().BeFalse();
    }
}
