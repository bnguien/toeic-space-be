using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using ToeicSpace.Content.Domain.Rules;
using static ToeicSpace.Content.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Metrics over word reviews, mastered words and the learners' saved words.</summary>
public static class VocabularyMetrics
{
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeReviewsAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var byStage = request.Has(AnalyticsDimensions.Stage);
        var partials = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetReviewPartialsAsync(request.Range!, byStage, take, token),
            cancellationToken);
        var stages = byStage ? await LoadStagesAsync(repository, cancellationToken) : [];

        var name = request.Metric.Name;

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (dimension, partial) => dimension == AnalyticsDimensions.Stage
                ? StageOf(stages, partial.WordId)
                : AnalyticsGrouping.TimeBucket(dimension, partial.Day),
            group =>
            {
                var learners = AnalyticsGrouping.Learners(group, partial => partial.LearnerId);

                switch (name)
                {
                    case VocabularyReviews:
                        var reviews = group.Sum(partial => partial.Reviews);
                        return (reviews, reviews, learners);
                    case VocabularyReviewAccuracy:
                        var graded = group.Sum(partial => partial.Graded);
                        return (AnalyticsGrouping.Ratio(group.Sum(partial => partial.Correct), graded), graded, learners);
                    case VocabularyReviewRetention:
                        var scheduled = group.Sum(partial => partial.Scheduled);
                        return (AnalyticsGrouping.Ratio(group.Sum(partial => partial.Recalled), scheduled), scheduled, learners);
                    default:
                        throw new InvalidOperationException($"Metric '{name}' is not a review metric.");
                }
            });
    }

    /// <summary>Distinct (learner, word) pairs that reached the mastered interval; a word mastered twice counts once.</summary>
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeWordsLearnedAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var mastered = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetMasteredWordsAsync(
                request.Range!, SpacedRepetitionScheduler.MasteredIntervalDays, take, token),
            cancellationToken);
        var stages = request.Has(AnalyticsDimensions.Stage) ? await LoadStagesAsync(repository, cancellationToken) : [];

        return AnalyticsGrouping.Aggregate(mastered, request.Dimensions,
            (dimension, fact) => dimension == AnalyticsDimensions.Stage
                ? StageOf(stages, fact.WordId)
                : AnalyticsGrouping.TimeBucket(dimension, fact.ReviewedAt),
            group =>
            {
                var words = group.Select(fact => (fact.LearnerId, fact.WordId)).Distinct().Count();
                var learners = AnalyticsGrouping.Learners(group, fact => fact.LearnerId);
                return (words, learners, learners);
            });
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeSavedWordsAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetSavedWordPartialsAsync(request.Range!, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (dimension, partial) => AnalyticsGrouping.TimeBucket(dimension, partial.Day),
            group =>
            {
                var words = group.Sum(partial => partial.Words);
                return (words, words, AnalyticsGrouping.Learners(group, partial => partial.LearnerId));
            });
    }

    /// <summary>
    /// Stage of each roadmap word. A word listed in several stages belongs to the earliest one on the roadmap;
    /// custom words belong to none.
    /// </summary>
    public static Dictionary<Guid, Guid> StagesByWord(IEnumerable<WordStage> memberships)
        => memberships
            .GroupBy(membership => membership.WordId)
            .ToDictionary(
                group => group.Key,
                group => group.OrderBy(membership => membership.StageOrder).ThenBy(membership => membership.StageId).First().StageId);

    private static async Task<Dictionary<Guid, Guid>> LoadStagesAsync(
        IContentAnalyticsRepository repository,
        CancellationToken cancellationToken)
        => StagesByWord(await AnalyticsExecution.LoadAsync(repository.GetWordStagesAsync, cancellationToken));

    private static object? StageOf(IReadOnlyDictionary<Guid, Guid> stages, Guid? wordId)
        => wordId is { } id && stages.TryGetValue(id, out var stage) ? stage : null;
}
