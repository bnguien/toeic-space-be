using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Models.Attempts;
using ToeicSpace.Assessment.Domain.Rules;

namespace ToeicSpace.Assessment.Application.Services.Attempts;

/// <summary>
/// Grades an attempt from its snapshot answer key. Scaled scores follow <see cref="ToeicScoreConversion"/> and are
/// only reported for a section of a test that the attempt covers completely.
/// </summary>
public static class AttemptScoreCalculator
{
    public static AttemptScoreDto Score(AttemptContent content)
    {
        var listening = content.Questions.Where(entry => IsListening(entry.Question.Part)).ToList();
        var reading = content.Questions.Where(entry => !IsListening(entry.Question.Part)).ToList();
        var listeningCorrect = listening.Count(entry => entry.IsCorrect == true);
        var readingCorrect = reading.Count(entry => entry.IsCorrect == true);
        var (listeningScaled, readingScaled, totalScaled) = Scaled(
            content.Source.Kind,
            AttemptParts.Parse(content.Attempt.SelectedParts),
            listeningCorrect, listening.Count,
            readingCorrect, reading.Count);

        return new AttemptScoreDto(
            content.Questions.Count,
            content.Questions.Count(entry => entry.IsAnswered),
            listeningCorrect + readingCorrect,
            listeningCorrect,
            listening.Count,
            readingCorrect,
            reading.Count,
            listeningScaled,
            readingScaled,
            totalScaled,
            totalScaled is { } total ? Cefr(total) : null);
    }

    public static IReadOnlyList<PartScoreDto> ByPart(AttemptContent content)
        => content.Questions
            .GroupBy(entry => entry.Question.Part)
            .OrderBy(group => group.Key)
            .Select(group => new PartScoreDto(
                (int)group.Key,
                group.Count(),
                group.Count(entry => entry.IsAnswered),
                group.Count(entry => entry.IsCorrect == true)))
            .ToList();

    /// <summary>
    /// Section scores need every part of that section of a test; the total needs both sections.
    /// </summary>
    public static (int? Listening, int? Reading, int? Total) Scaled(
        AttemptSourceKind sourceKind,
        IReadOnlyCollection<ToeicPart> parts,
        int listeningCorrect,
        int listeningTotal,
        int readingCorrect,
        int readingTotal)
    {
        if (sourceKind != AttemptSourceKind.Test)
        {
            return (null, null, null);
        }

        int? listening = listeningTotal > 0 && ToeicScoreConversion.CoversSection(parts, ToeicSection.Listening)
            ? ToeicScoreConversion.ScaledScore(listeningCorrect, listeningTotal)
            : null;
        int? reading = readingTotal > 0 && ToeicScoreConversion.CoversSection(parts, ToeicSection.Reading)
            ? ToeicScoreConversion.ScaledScore(readingCorrect, readingTotal)
            : null;

        return (listening, reading, listening + reading);
    }

    /// <summary>Scaled scores of a submitted attempt from its stored columns (no snapshot needed).</summary>
    public static (int? Listening, int? Reading, int? Total) StoredScaled(ToeicAttempt attempt)
    {
        if (attempt.Status != AttemptStatus.Completed || attempt.TestId == null)
        {
            return (null, null, null);
        }

        var parts = AttemptParts.Parse(attempt.SelectedParts);
        int? listening = ToeicScoreConversion.CoversSection(parts, ToeicSection.Listening) ? attempt.ListeningScore : null;
        int? reading = ToeicScoreConversion.CoversSection(parts, ToeicSection.Reading) ? attempt.ReadingScore : null;

        return (listening, reading, listening + reading);
    }

    public static CefrDto Cefr(int totalScaled)
    {
        var band = ToeicScoreConversion.Cefr(totalScaled);

        return new CefrDto(band.Level, band.Title);
    }

    private static bool IsListening(ToeicPart part) => ToeicPartRules.GetSection(part) == ToeicSection.Listening;
}
