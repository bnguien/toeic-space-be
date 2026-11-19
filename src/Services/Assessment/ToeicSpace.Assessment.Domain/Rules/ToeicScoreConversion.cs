using ToeicSpace.Assessment.Domain.Enums;

namespace ToeicSpace.Assessment.Domain.Rules;

/// <summary>
/// Estimated TOEIC scaled scores (5–495 per section) and the CEFR band of a total score.
/// Port of StudyChill <c>exam/services/grading.py</c> so both products report the same numbers.
/// </summary>
public static class ToeicScoreConversion
{
    public const int MinimumSectionScore = 5;

    public const int MaximumSectionScore = 495;

    private static readonly ToeicPart[] ListeningParts = [ToeicPart.Part1, ToeicPart.Part2, ToeicPart.Part3, ToeicPart.Part4];

    private static readonly ToeicPart[] ReadingParts = [ToeicPart.Part5, ToeicPart.Part6, ToeicPart.Part7];

    /// <summary>
    /// ratio ≤ 5% → 5, ratio ≥ 96% → 495, otherwise a linear scale rounded to a multiple of 5.
    /// Uses banker's rounding like Python's <c>round</c>.
    /// </summary>
    public static int ScaledScore(int correct, int total)
    {
        if (total <= 0)
        {
            return MinimumSectionScore;
        }

        var ratio = (double)correct / total;

        if (ratio <= 0.05)
        {
            return MinimumSectionScore;
        }

        if (ratio >= 0.96)
        {
            return MaximumSectionScore;
        }

        var scaled = (int)Math.Round((ratio * 490 + 5) / 5, MidpointRounding.ToEven) * 5;

        return Math.Clamp(scaled, MinimumSectionScore, MaximumSectionScore);
    }

    /// <summary>
    /// A section score is only meaningful when the attempt covers every part of that section of a full test.
    /// </summary>
    public static bool CoversSection(IReadOnlyCollection<ToeicPart> parts, ToeicSection section)
        => (section == ToeicSection.Listening ? ListeningParts : ReadingParts).All(parts.Contains);

    public static CefrBand Cefr(int totalScaled) => totalScaled switch
    {
        >= 905 => new CefrBand("C2", "Thành thạo cao cấp"),
        >= 785 => new CefrBand("C1", "Vững vàng cao cấp"),
        >= 605 => new CefrBand("B2", "Trung cấp trên"),
        >= 405 => new CefrBand("B1", "Trung cấp cơ bản"),
        >= 255 => new CefrBand("A2", "Sơ cấp"),
        _ => new CefrBand("A1", "Cơ bản")
    };
}
