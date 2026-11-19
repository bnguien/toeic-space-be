namespace ToeicSpace.Assessment.Application.Dtos;

/// <summary>Everything a caller needs to build a valid analytics query.</summary>
public sealed record AnalyticsCatalogDto(
    IReadOnlyList<AnalyticsMetricDto> Metrics,
    IReadOnlyList<AnalyticsNameDto> Dimensions,
    IReadOnlyList<AnalyticsNameDto> Filters,
    IReadOnlyList<string> EventTypes,
    AnalyticsLimitsDto Limits,
    IReadOnlyList<AnalyticsPlannedMetricDto> Planned);

/// <param name="CountUnit">What <c>n</c> counts in a result row.</param>
/// <param name="RequiredDimension">Always part of the grouping, added when missing from <c>by</c>.</param>
/// <param name="Personal">Computed per learner before being aggregated.</param>
/// <param name="Suppressed">Groups with fewer than the minimum number of learners return a null value.</param>
/// <param name="Sort">dimensions, value_asc or value_desc.</param>
public sealed record AnalyticsMetricDto(
    string Name,
    string Description,
    string Unit,
    string CountUnit,
    IReadOnlyList<string> Dimensions,
    IReadOnlyList<string> Filters,
    string? RequiredDimension,
    string Visibility,
    bool Personal,
    bool Suppressed,
    string Sort);

public sealed record AnalyticsNameDto(
    string Name,
    string Description);

public sealed record AnalyticsPlannedMetricDto(
    string Name,
    string Service,
    string Description,
    string Visibility);

public sealed record AnalyticsLimitsDto(
    int MinimumLearnersPerGroup,
    int MaxDimensions,
    int DefaultRowLimit,
    int MaxRowLimit,
    int DefaultEventLimit,
    int MaxEventLimit,
    int DefaultRangeDays,
    int MaxRangeDays,
    int TimeoutSeconds);

/// <param name="Rows">One object per group: each dimension of <c>by</c>, then <c>value</c> (null when suppressed or undefined) and <c>n</c> (0 when suppressed).</param>
/// <param name="Suppressed">Number of groups hidden because they hold fewer than 5 distinct learners.</param>
/// <param name="Truncated">True when more groups existed than <c>limit</c>.</param>
/// <param name="Labels">Codes of the tests and practice sets referenced by the rows, keyed by ID.</param>
public sealed record AnalyticsResultDto(
    string Metric,
    IReadOnlyList<string> By,
    string Unit,
    string CountUnit,
    DateOnly? From,
    DateOnly? To,
    IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows,
    int Suppressed,
    bool Truncated,
    DateTime GeneratedAt,
    IReadOnlyDictionary<string, string>? Labels);

/// <param name="NextCursor">Pass back as <c>cursor</c> for the next page; null on the last page.</param>
public sealed record AnalyticsEventsDto(
    string Type,
    DateOnly From,
    DateOnly To,
    IReadOnlyList<AnalyticsEventDto> Rows,
    string? NextCursor,
    DateTime GeneratedAt);

/// <summary>
/// One anonymised event. <c>learner</c> and <c>attempt</c> are keyed pseudonyms, never raw IDs.
/// Attempt events fill the score fields, answer events the question fields.
/// </summary>
public sealed record AnalyticsEventDto(
    string Learner,
    string Attempt,
    Guid? TestId,
    AttemptMode Mode,
    DateTime StartedAt,
    AttemptStatus? Status = null,
    DateTime? EndedAt = null,
    int? DurationSeconds = null,
    int? TotalScore = null,
    int? ListeningScore = null,
    int? ReadingScore = null,
    int? TotalCorrect = null,
    int? TotalQuestions = null,
    Guid? QuestionId = null,
    ToeicPart? Part = null,
    ToeicSection? Section = null,
    QuestionDifficulty? Difficulty = null,
    bool? Answered = null,
    bool? Correct = null,
    int? TimeSpentSeconds = null);
