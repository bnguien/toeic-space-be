using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Analytics.Common;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

/// <summary>
/// The fixed EF Core queries behind the analytics metrics. Request values only become typed parameters
/// (dates, IDs, enum values); grouping keys and columns never come from input. Names, emails and the
/// read models of people are never read.
/// </summary>
public sealed class ClassroomAnalyticsReader : IClassroomAnalyticsReader
{
    private const int RowCap = AnalyticsLimits.MaxFactRows + 1;

    private readonly ClassroomDbContext _context;

    public ClassroomAnalyticsReader(ClassroomDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<ClassFact>> GetClassesAsync(
        ClassFactFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _context.Classes
            .AsNoTracking()
            .Where(entity => entity.Status != ClassStatus.Deleted);

        if (filter.CreatedFrom is { } createdFrom)
        {
            query = query.Where(entity => entity.CreatedAt >= createdFrom);
        }

        if (filter.CreatedToExclusive is { } createdTo)
        {
            query = query.Where(entity => entity.CreatedAt < createdTo);
        }

        if (filter.StartFrom is { } startFrom)
        {
            query = query.Where(entity => entity.StartDate != null && entity.StartDate >= startFrom);
        }

        if (filter.StartTo is { } startTo)
        {
            query = query.Where(entity => entity.StartDate != null && entity.StartDate <= startTo);
        }

        if (filter.Statuses is { Count: > 0 } statuses)
        {
            var allowed = statuses.ToList();
            query = query.Where(entity => allowed.Contains(entity.Status));
        }

        return await query
            .Select(entity => new ClassFact(entity.Id, entity.Status, entity.CreatedAt, entity.StartDate))
            .Take(RowCap)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<EnrollmentFact>> GetEnrollmentsAsync(
        EnrollmentFactFilter filter,
        CancellationToken cancellationToken)
    {
        var query = _context.ClassEnrollments
            .AsNoTracking()
            .Where(enrollment => enrollment.Class.Status != ClassStatus.Deleted);

        if (filter.EnrolledFrom is { } enrolledFrom)
        {
            query = query.Where(enrollment => enrollment.EnrolledAt >= enrolledFrom);
        }

        if (filter.EnrolledToExclusive is { } enrolledTo)
        {
            query = query.Where(enrollment => enrollment.EnrolledAt < enrolledTo);
        }

        if (filter.ClassStartFrom is { } startFrom)
        {
            query = query.Where(enrollment => enrollment.Class.StartDate != null && enrollment.Class.StartDate >= startFrom);
        }

        if (filter.ClassStartTo is { } startTo)
        {
            query = query.Where(enrollment => enrollment.Class.StartDate != null && enrollment.Class.StartDate <= startTo);
        }

        if (filter.ClassId is { } classId)
        {
            query = query.Where(enrollment => enrollment.ClassId == classId);
        }

        // Current status: Active only while the row is not withdrawn (same rule as ClassEnrollment.IsCurrent).
        if (filter.Status == EnrollmentStatus.Active)
        {
            query = query.Where(enrollment => enrollment.Status == EnrollmentStatus.Active && enrollment.WithdrawnAt == null);
        }
        else if (filter.Status == EnrollmentStatus.Withdrawn)
        {
            query = query.Where(enrollment => enrollment.Status != EnrollmentStatus.Active || enrollment.WithdrawnAt != null);
        }

        var rows = await query
            .Select(enrollment => new EnrollmentRow(
                enrollment.ClassId,
                enrollment.StudentId,
                enrollment.Status,
                enrollment.WithdrawnAt,
                enrollment.EnrolledAt,
                enrollment.Class.StartDate))
            .Take(RowCap)
            .ToListAsync(cancellationToken);

        return rows.Select(row => row.ToFact()).ToList();
    }

    public async Task<IReadOnlyList<TeacherAssignmentFact>> GetCurrentAssignmentsAsync(
        IReadOnlyCollection<ClassStatus> classStatuses,
        CancellationToken cancellationToken)
    {
        var allowed = classStatuses.ToList();

        return await _context.ClassInstructors
            .AsNoTracking()
            .Where(instructor => instructor.UnassignedAt == null
                && instructor.Class.Status != ClassStatus.Deleted
                && allowed.Contains(instructor.Class.Status))
            .Select(instructor => new TeacherAssignmentFact(instructor.TeacherId, instructor.ClassId))
            .Take(RowCap)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetClassCodesAsync(
        IReadOnlyCollection<Guid> classIds,
        CancellationToken cancellationToken)
    {
        var ids = classIds.ToList();

        return await _context.Classes
            .AsNoTracking()
            .Where(entity => ids.Contains(entity.Id))
            .Select(entity => new { entity.Id, entity.Code })
            .ToDictionaryAsync(entity => entity.Id, entity => entity.Code, cancellationToken);
    }

    /// <summary>Raw enrollment columns; the current status is derived after loading.</summary>
    private sealed record EnrollmentRow(
        Guid ClassId,
        Guid StudentId,
        EnrollmentStatus Status,
        DateTime? WithdrawnAt,
        DateTime EnrolledAt,
        DateOnly? ClassStartDate)
    {
        public EnrollmentFact ToFact() => new(
            ClassId,
            StudentId,
            Status == EnrollmentStatus.Active && WithdrawnAt is null ? EnrollmentStatus.Active : EnrollmentStatus.Withdrawn,
            EnrolledAt,
            ClassStartDate);
    }
}
