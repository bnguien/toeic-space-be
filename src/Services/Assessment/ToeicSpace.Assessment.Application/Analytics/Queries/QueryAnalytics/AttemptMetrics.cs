using ToeicSpace.Assessment.Application.Analytics.Common;
using ToeicSpace.Assessment.Application.Interfaces.Persistence;
using static ToeicSpace.Assessment.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Metrics over attempts and over learner cohorts (first attempt ever).</summary>
public static class AttemptMetrics
{
    private static readonly HashSet<string> CompletedOnly =
        [AvgScore, MedianScore, AvgListeningScore, AvgReadingScore, ScoreDistribution, AvgDuration, ScoreGain];

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Metric.Name;
        var facts = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.Attempts(context, request, CompletedOnly.Contains(name)),
            cancellationToken);

        if (name == AvgDuration)
        {
            facts = facts.Where(fact => fact.DurationSeconds > 0).ToList();
        }

        return AnalyticsGrouping.Aggregate(facts, request.Dimensions, Resolve, Measure(name));
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeCohortsAsync(
        IAssessmentDbContext context,
        AnalyticsRequest request,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        var learners = await AnalyticsExecution.LoadAsync(
            AnalyticsSources.FirstAttempts(context, request),
            cancellationToken);

        if (request.Metric.Name == NewLearners)
        {
            return AnalyticsGrouping.Aggregate(learners, request.Dimensions,
                (dimension, learner) => AnalyticsGrouping.TimeBucket(dimension, learner.FirstStart),
                group => (group.Count, group.Count, group.Count));
        }

        // Retention: only learners whose 7-day follow-up window has fully passed.
        var lastEligibleDay = today.AddDays(-7);
        var cohort = learners
            .Where(learner => DateOnly.FromDateTime(learner.FirstStart) <= lastEligibleDay)
            .ToList();

        var activeDays = cohort.Count == 0
            ? []
            : await AnalyticsExecution.LoadAsync(
                AnalyticsSources.ActiveDays(context, request.Range!.FromUtc, request.Range.ToUtcExclusive.AddDays(8)),
                cancellationToken);

        var daysByLearner = activeDays
            .GroupBy(day => day.UserId)
            .ToDictionary(group => group.Key, group => group.Select(day => DateOnly.FromDateTime(day.Day)).ToHashSet());

        bool Returned(LearnerFirstAttempt learner)
        {
            var first = DateOnly.FromDateTime(learner.FirstStart);
            return daysByLearner.TryGetValue(learner.UserId, out var days)
                && days.Any(day => day > first && day <= first.AddDays(7));
        }

        return AnalyticsGrouping.Aggregate(cohort, request.Dimensions,
            (dimension, learner) => AnalyticsGrouping.TimeBucket(dimension, learner.FirstStart),
            group => (AnalyticsGrouping.Ratio(group.Count(Returned), group.Count), group.Count, group.Count));
    }

    private static object? Resolve(string dimension, AttemptFact fact) => dimension switch
    {
        AnalyticsDimensions.Test => fact.TestId,
        AnalyticsDimensions.Mode => fact.Mode,
        AnalyticsDimensions.Status => fact.Status,
        AnalyticsDimensions.ScoreBand => AnalyticsGrouping.ScoreBand(fact.TotalScore),
        _ => AnalyticsGrouping.TimeBucket(dimension, fact.StartTime)
    };

    private static Func<IReadOnlyCollection<AttemptFact>, (double? Value, int Count, int Learners)> Measure(string metric)
        => metric switch
        {
            AttemptCount or ScoreDistribution => group => (group.Count, group.Count, Learners(group)),
            ActiveLearners or TestPopularity => group =>
            {
                var learners = Learners(group);
                return (learners, learners, learners);
            },
            AttemptsPerLearner => group =>
            {
                var learners = Learners(group);
                return (AnalyticsGrouping.Ratio(group.Count, learners), learners, learners);
            },
            CompletionRate => group => (
                AnalyticsGrouping.Ratio(group.Count(fact => fact.Status == AttemptStatus.Completed), group.Count),
                group.Count,
                Learners(group)),
            AbandonRate => group => (
                AnalyticsGrouping.Ratio(group.Count(fact => fact.Status == AttemptStatus.Abandoned), group.Count),
                group.Count,
                Learners(group)),
            AvgScore => group => (group.Average(fact => fact.TotalScore), group.Count, Learners(group)),
            MedianScore => group => (AnalyticsGrouping.Median(group.Select(fact => fact.TotalScore)), group.Count, Learners(group)),
            AvgListeningScore => group => (group.Average(fact => fact.ListeningScore), group.Count, Learners(group)),
            AvgReadingScore => group => (group.Average(fact => fact.ReadingScore), group.Count, Learners(group)),
            AvgDuration => group => (group.Average(fact => fact.DurationSeconds), group.Count, Learners(group)),
            ReturningRate => group =>
            {
                var learners = Learners(group);
                var returning = group
                    .GroupBy(fact => fact.UserId)
                    .Count(learner => learner.Select(fact => fact.StartTime.Date).Distinct().Count() >= 2);
                return (AnalyticsGrouping.Ratio(returning, learners), learners, learners);
            },
            ScoreGain => MeasureScoreGain,
            _ => throw new InvalidOperationException($"Metric '{metric}' is not an attempt metric.")
        };

    /// <summary>Latest minus first completed score of each learner with at least two completed attempts.</summary>
    private static (double? Value, int Count, int Learners) MeasureScoreGain(IReadOnlyCollection<AttemptFact> group)
    {
        var gains = group
            .GroupBy(fact => fact.UserId)
            .Where(learner => learner.Count() >= 2)
            .Select(learner =>
            {
                var ordered = learner.OrderBy(fact => fact.StartTime).ToList();
                return ordered[^1].TotalScore - ordered[0].TotalScore;
            })
            .ToList();

        return (gains.Count == 0 ? null : gains.Average(), gains.Count, gains.Count);
    }

    private static int Learners(IEnumerable<AttemptFact> group)
        => group.Select(fact => fact.UserId).Distinct().Count();
}
