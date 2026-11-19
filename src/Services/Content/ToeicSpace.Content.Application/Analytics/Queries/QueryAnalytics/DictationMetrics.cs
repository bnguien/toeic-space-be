using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using static ToeicSpace.Content.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;

/// <summary>
/// Metrics over dictation progress. Each (learner, sentence) row keeps only its latest attempt time, so a sentence
/// counts once, on the day of its latest attempt.
/// </summary>
public static class DictationMetrics
{
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetDictationPartialsAsync(request.Range!, request.DictationCourseId, take, token),
            cancellationToken);

        var name = request.Metric.Name;

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (dimension, partial) => dimension == AnalyticsDimensions.Course
                ? partial.CourseId
                : AnalyticsGrouping.TimeBucket(dimension, partial.Day),
            group =>
            {
                var items = group.Sum(partial => partial.Items);
                var learners = AnalyticsGrouping.Learners(group, partial => partial.LearnerId);

                return name switch
                {
                    DictationAttempts => (items, items, learners),
                    DictationCompletion => (AnalyticsGrouping.Ratio(group.Sum(partial => partial.Passed), items), items, learners),
                    _ => throw new InvalidOperationException($"Metric '{name}' is not a dictation metric.")
                };
            });
    }
}
