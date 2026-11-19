namespace ToeicSpace.Assessment.Application.DTOs.Attempts;

/// <param name="CorrectRate">0..1 over graded answers.</param>
/// <param name="ScoreTrend">Last 20 completed FullTest attempts, oldest first.</param>
/// <param name="Recent">The 8 most recent attempts that were not abandoned.</param>
public sealed record PracticeStatsDto(
    int TestsCompleted,
    int TestsTotal,
    int QuestionsAnswered,
    double CorrectRate,
    IReadOnlyList<PartStatsDto> ByPart,
    IReadOnlyList<ScoreTrendPointDto> ScoreTrend,
    IReadOnlyList<AttemptSummaryDto> Recent);
