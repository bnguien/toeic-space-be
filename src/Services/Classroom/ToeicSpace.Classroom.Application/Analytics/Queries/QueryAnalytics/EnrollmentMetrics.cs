using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Enums;
using static ToeicSpace.Classroom.Application.Analytics.Common.AnalyticsCatalog;

namespace ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;

/// <summary>Metrics over enrollments and over the classes that started in the range (class cohorts).</summary>
public static class EnrollmentMetrics
{
    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeAsync(
        IClassroomAnalyticsReader reader,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var name = request.Metric.Name;

        // enrollment_status_mix describes the current state; the other enrollment metrics use EnrolledAt.
        var filter = name == EnrollmentStatusMix
            ? new EnrollmentFactFilter(ClassId: request.ClassId)
            : new EnrollmentFactFilter(
                EnrolledFrom: request.Range!.FromUtc,
                EnrolledToExclusive: request.Range.ToUtcExclusive,
                ClassId: request.ClassId,
                Status: request.EnrollmentStatus);

        var enrollments = await LoadEnrollmentsAsync(reader, filter, cancellationToken);

        return name switch
        {
            ClassEnrollment or EnrollmentStatusMix => AnalyticsGrouping.Aggregate(enrollments, request.Dimensions,
                (dimension, fact) => dimension switch
                {
                    AnalyticsDimensions.Status => fact.Status,
                    AnalyticsDimensions.Class => fact.ClassId,
                    _ => AnalyticsGrouping.TimeBucket(dimension, fact.EnrolledAt)
                },
                group => (group.Count, group.Count, Learners(group))),
            EnrolledLearners => AnalyticsGrouping.Aggregate(enrollments, request.Dimensions,
                (dimension, fact) => AnalyticsGrouping.TimeBucket(dimension, fact.EnrolledAt),
                group =>
                {
                    var learners = Learners(group);
                    return (learners, learners, learners);
                }),
            _ => throw new InvalidOperationException($"Metric '{name}' is not an enrollment metric.")
        };
    }

    public static async Task<IReadOnlyList<AnalyticsGroup>> ComputeCohortsAsync(
        IClassroomAnalyticsReader reader,
        AnalyticsRequest request,
        CancellationToken cancellationToken)
    {
        var range = request.Range!;

        if (request.Metric.Name == WithdrawRate)
        {
            var enrollments = await LoadEnrollmentsAsync(reader, new EnrollmentFactFilter(
                ClassStartFrom: range.From,
                ClassStartTo: range.To,
                ClassId: request.ClassId), cancellationToken);

            return AnalyticsGrouping.Aggregate(enrollments, request.Dimensions,
                (dimension, fact) => dimension == AnalyticsDimensions.Class
                    ? fact.ClassId
                    : AnalyticsGrouping.TimeBucket(dimension, fact.ClassStartDate),
                group => (
                    AnalyticsGrouping.Ratio(group.Count(fact => fact.Status == EnrollmentStatus.Withdrawn), group.Count),
                    group.Count,
                    Learners(group)));
        }

        if (request.Metric.Name == ClassSizeAvg)
        {
            var classes = AnalyticsExecution.EnsureWithinLimit(await reader.GetClassesAsync(
                new ClassFactFilter(StartFrom: range.From, StartTo: range.To), cancellationToken));

            var current = classes.Count == 0
                ? []
                : await LoadEnrollmentsAsync(reader, new EnrollmentFactFilter(
                    ClassStartFrom: range.From,
                    ClassStartTo: range.To,
                    Status: EnrollmentStatus.Active), cancellationToken);

            var studentsByClass = current
                .GroupBy(fact => fact.ClassId)
                .ToDictionary(group => group.Key, group => group.Select(fact => fact.StudentId).ToList());

            // Classes without anyone enrolled count as size 0.
            return AnalyticsGrouping.Aggregate(classes, request.Dimensions,
                (dimension, fact) => AnalyticsGrouping.TimeBucket(dimension, fact.StartDate),
                group =>
                {
                    var students = group
                        .SelectMany(fact => studentsByClass.GetValueOrDefault(fact.ClassId) ?? [])
                        .ToList();
                    return ((double)students.Count / group.Count, group.Count, students.Distinct().Count());
                });
        }

        throw new InvalidOperationException($"Metric '{request.Metric.Name}' is not a class cohort metric.");
    }

    private static async Task<IReadOnlyList<EnrollmentFact>> LoadEnrollmentsAsync(
        IClassroomAnalyticsReader reader,
        EnrollmentFactFilter filter,
        CancellationToken cancellationToken)
        => AnalyticsExecution.EnsureWithinLimit(await reader.GetEnrollmentsAsync(filter, cancellationToken));

    private static int Learners(IEnumerable<EnrollmentFact> group)
        => group.Select(fact => fact.StudentId).Distinct().Count();
}
