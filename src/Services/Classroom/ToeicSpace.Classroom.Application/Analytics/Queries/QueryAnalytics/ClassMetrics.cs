using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Enums;
using static ToeicSpace.Classroom.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Counts over classes and teacher assignments. No learner data is involved, so nothing is suppressed.</summary>
public static class ClassMetrics
{
    /// <summary>Classes that are open now: not started yet (Draft) or running (Active).</summary>
    public static readonly IReadOnlyList<ClassStatus> OpenStatuses = [ClassStatus.Draft, ClassStatus.Active];

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IClassroomAnalyticsReader reader,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Metric.Name;

        if (name == ClassCount)
        {
            var created = await LoadClassesAsync(reader, new ClassFactFilter(
                CreatedFrom: request.Range!.FromUtc,
                CreatedToExclusive: request.Range.ToUtcExclusive,
                Statuses: request.ClassStatus is { } status ? [status] : null), cancellationToken);

            return AnalyticsGrouping.Aggregate(created, request.Dimensions,
                (dimension, fact) => dimension == AnalyticsDimensions.Status
                    ? fact.Status
                    : AnalyticsGrouping.TimeBucket(dimension, fact.CreatedAt),
                Count);
        }

        if (name == ActiveClasses)
        {
            var open = await LoadClassesAsync(reader, new ClassFactFilter(Statuses: OpenStatuses), cancellationToken);

            return AnalyticsGrouping.Aggregate(open, request.Dimensions,
                (dimension, fact) => dimension == AnalyticsDimensions.Status
                    ? fact.Status
                    : AnalyticsGrouping.TimeBucket(dimension, fact.StartDate),
                Count);
        }

        throw new InvalidOperationException($"Metric '{name}' is not a class metric.");
    }

    /// <summary>Open classes per teacher; teachers appear only as an opaque 8-hex key.</summary>
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeTeacherLoadAsync(
        IClassroomAnalyticsReader reader,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var assignments = AnalyticsExecution.EnsureWithinLimit(
            await reader.GetCurrentAssignmentsAsync(OpenStatuses, cancellationToken));

        return AnalyticsGrouping.Aggregate(assignments, request.Dimensions,
            (_, fact) => AnalyticsGrouping.OpaqueId(AnalyticsDimensions.Teacher, fact.TeacherId),
            group =>
            {
                var classes = group.Select(fact => fact.ClassId).Distinct().Count();
                return (classes, classes, 0);
            });
    }

    private static async Task<IReadOnlyList<ClassFact>> LoadClassesAsync(
        IClassroomAnalyticsReader reader,
        ClassFactFilter filter,
        CancellationToken cancellationToken)
        => AnalyticsExecution.EnsureWithinLimit(await reader.GetClassesAsync(filter, cancellationToken));

    private static (double? Value, int Count, int Learners) Count(IReadOnlyCollection<ClassFact> group)
        => (group.Count, group.Count, 0);
}
