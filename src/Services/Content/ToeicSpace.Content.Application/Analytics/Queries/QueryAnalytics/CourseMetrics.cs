using ToeicSpace.Content.Application.Analytics.Common;
using ToeicSpace.Content.Application.Interfaces.Repositories;
using static ToeicSpace.Content.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Counts over the current course catalogue. No learner data is involved, so nothing is suppressed.</summary>
public static class CourseMetrics
{
    public static Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
        => request.Metric.Name switch
        {
            CourseCount => CountCoursesAsync(repository, request, cancellationToken),
            LessonCount => CountLessonsAsync(repository, request, cancellationToken),
            _ => throw new InvalidOperationException($"Metric '{request.Metric.Name}' is not a course metric.")
        };

    private static async Task<IReadOnlyList<AnalyticsGroup>> CountCoursesAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var courses = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetCoursesAsync(request.CourseStatus, request.Level, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(courses, request.Dimensions,
            (dimension, course) => dimension switch
            {
                AnalyticsDimensions.Status => course.Status,
                AnalyticsDimensions.Level => course.Level,
                AnalyticsDimensions.ExamType => course.ExamType,
                _ => AnalyticsGrouping.TimeBucket(dimension, course.CreatedAt)
            },
            group => (group.Count, group.Count, 0));
    }

    private static async Task<IReadOnlyList<AnalyticsGroup>> CountLessonsAsync(
        IContentAnalyticsRepository repository,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var counts = await AnalyticsExecution.LoadAsync(
            (take, token) => repository.GetLessonCountsAsync(request.CourseId, request.LessonStatus, take, token),
            cancellationToken);

        return AnalyticsGrouping.Aggregate(counts, request.Dimensions,
            (dimension, row) => dimension switch
            {
                AnalyticsDimensions.Course => row.CourseId,
                AnalyticsDimensions.Status => row.Status,
                _ => null
            },
            group =>
            {
                var lessons = group.Sum(row => row.Lessons);
                return (lessons, lessons, 0);
            });
    }
}
