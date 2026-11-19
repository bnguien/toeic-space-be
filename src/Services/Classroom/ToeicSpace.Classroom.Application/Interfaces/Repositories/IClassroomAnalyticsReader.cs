using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Interfaces.Repositories;

/// <summary>
/// Fixed read-only queries behind the analytics metrics. Soft-deleted classes (and their enrollments and
/// assignments) are never returned. Each list holds at most <see cref="AnalyticsLimits.MaxFactRows"/> + 1 rows
/// so the caller can tell when a selection is too large.
/// </summary>
public interface IClassroomAnalyticsReader
{
    Task<IReadOnlyList<ClassFact>> GetClassesAsync(ClassFactFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<EnrollmentFact>> GetEnrollmentsAsync(EnrollmentFactFilter filter, CancellationToken cancellationToken);

    Task<IReadOnlyList<TeacherAssignmentFact>> GetCurrentAssignmentsAsync(
        IReadOnlyCollection<ClassStatus> classStatuses,
        CancellationToken cancellationToken);

    /// <summary>Class codes keyed by class ID, for the <c>labels</c> of a result.</summary>
    Task<IReadOnlyDictionary<Guid, string>> GetClassCodesAsync(
        IReadOnlyCollection<Guid> classIds,
        CancellationToken cancellationToken);
}
