using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Interfaces.Repositories;

namespace ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Metrics over grammar attempts, question results and topic progress.</summary>
public static class GrammarMetrics
{
    public const string TopicPrefix = "topic-";
    public const string StagePrefix = "stage-";

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAttemptsAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var attempts = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetGrammarAttemptsAsync(request.Range!, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(attempts, request.Dimensions,
            (dimension, attempt) => dimension == AnalyticsDimensions.Document
                ? DocumentOf(attempt.Topic, attempt.Stage)
                : AnalyticsGrouping.TimeBucket(dimension, attempt.CreatedAt),
            group => (group.Count, group.Count, AnalyticsGrouping.Learners(group, attempt => attempt.LearnerId)));
    }

    /// <summary>First-answer accuracy of answered questions, grouped by the main topic of each question.</summary>
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAccuracyAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var partials = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetGrammarAnswerPartialsAsync(request.Range!, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(partials, request.Dimensions,
            (dimension, partial) => dimension == AnalyticsDimensions.Document
                ? DocumentOf(partial.Topic, null)
                : AnalyticsGrouping.TimeBucket(dimension, partial.Day),
            group =>
            {
                var answers = group.Sum(partial => partial.Answers);
                return (AnalyticsGrouping.Ratio(group.Sum(partial => partial.Correct), answers), answers,
                    AnalyticsGrouping.Learners(group, partial => partial.LearnerId));
            });
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeLessonCompletionAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var lessons = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetGrammarLessonsAsync(request.Range!, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(lessons, request.Dimensions,
            (dimension, lesson) => dimension == AnalyticsDimensions.Document
                ? DocumentOf(lesson.Topic, null)
                : AnalyticsGrouping.TimeBucket(dimension, lesson.UpdatedAt),
            group => (AnalyticsGrouping.Ratio(group.Count(lesson => lesson.Completed), group.Count), group.Count,
                AnalyticsGrouping.Learners(group, lesson => lesson.LearnerId)));
    }

    /// <summary>Document code of a topic (<c>topic-04</c>) or of a stage test (<c>stage-02</c>).</summary>
    public static string? DocumentOf(string? topic, string? stage)
        => !string.IsNullOrEmpty(topic) ? TopicPrefix + topic
            : !string.IsNullOrEmpty(stage) ? StagePrefix + stage
            : null;
}
