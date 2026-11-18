using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using static ToeicSpace.Content.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Metrics over vocabulary study sessions and over learner activity of the whole service.</summary>
public static class StudyMetrics
{
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeSessionsAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var sessions = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetStudySessionsAsync(request.Range!, take, token),
            cancellationToken);

        var name = request.Metric.Name;

        return AnalyticsGrouping.Aggregate(sessions, request.Dimensions,
            (dimension, session) => dimension switch
            {
                AnalyticsDimensions.Kind => session.Mode,
                AnalyticsDimensions.Source => session.Source,
                _ => AnalyticsGrouping.TimeBucket(dimension, session.StartedAt)
            },
            group =>
            {
                var learners = AnalyticsGrouping.Learners(group, session => session.LearnerId);

                return name switch
                {
                    StudySessions => (group.Count, group.Count, learners),
                    StudyTime => (group.Sum(Minutes), learners, learners),
                    _ => throw new InvalidOperationException($"Metric '{name}' is not a study session metric.")
                };
            });
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeActiveLearnersAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var days = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetActiveLearnerDaysAsync(request.Range!, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(days, request.Dimensions,
            (dimension, day) => AnalyticsGrouping.TimeBucket(dimension, day.Day),
            group =>
            {
                var learners = AnalyticsGrouping.Learners(group, day => day.LearnerId);
                return (learners, learners, learners);
            });
    }

    /// <summary>Minutes from the start to the last answer or completion, capped so forgotten sessions do not count as study.</summary>
    public static double Minutes(StudySessionFact session)
        => Math.Clamp((session.LastActivityAt - session.StartedAt).TotalMinutes, 0, AnalyticsLimits.MaxSessionMinutes);
}
