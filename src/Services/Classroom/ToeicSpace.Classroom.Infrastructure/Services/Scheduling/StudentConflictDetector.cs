using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed class StudentConflictDetector(ClassroomDbContext db) : IStudentConflictDetector
{
    public async Task<StudentConflictPreview> PreviewAsync(IReadOnlyList<StudentConflictSession> proposed,
        IReadOnlyCollection<Guid>? enrollingStudents, ConflictDetailsRequest? details, CancellationToken ct)
    {
        if (details is { } paging && (paging.Page < 1 || paging.PageSize is < 1 or > 100 ||
            paging.Page > int.MaxValue / paging.PageSize))
            throw new ClassRuleException("invalid_pagination", "Valid pagination is required.");
        if (proposed.Count == 0) return Empty(details);
        var classIds = proposed.Select(x => x.ClassId).Distinct().ToArray();
        var enrolled = await db.ClassEnrollments.AsNoTracking()
            .Where(x => classIds.Contains(x.ClassId) && x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null)
            .Select(x => new { x.ClassId, x.StudentId }).ToListAsync(ct);
        var studentIds = enrollingStudents is null
            ? enrolled.Select(x => x.StudentId).Distinct().ToArray()
            : enrollingStudents.Distinct().ToArray();
        if (studentIds.Length == 0) return Empty(details);
        var byClass = classIds.ToDictionary(id => id, id => enrollingStudents is null
            ? enrolled.Where(x => x.ClassId == id).Select(x => x.StudentId).ToHashSet()
            : studentIds.ToHashSet());
        var start = proposed.Min(x => x.StartAt);
        var end = proposed.Max(x => x.EndAt);
        var proposedIds = proposed.Select(x => x.SessionId).ToArray();
        // Fetch only the affected students' non-cancelled sessions in the proposed time range.
        var occupied = await (from session in db.ClassSessions.AsNoTracking()
                              join enrollment in db.ClassEnrollments.AsNoTracking() on session.ClassId equals enrollment.ClassId
                              join classroom in db.Classes.AsNoTracking() on session.ClassId equals classroom.Id
                              where studentIds.Contains(enrollment.StudentId) && enrollment.Status == EnrollmentStatus.Active &&
                                    enrollment.WithdrawnAt == null && classroom.Status != ClassStatus.Deleted &&
                                    session.Status != SessionStatus.Cancelled && !proposedIds.Contains(session.Id) &&
                                    session.StartAt < end && session.EndAt > start
                              select new { enrollment.StudentId, session.Id, session.ClassId, session.StartAt, session.EndAt })
            .ToListAsync(ct);

        var pairs = new HashSet<(Guid StudentId, Guid First, Guid Second)>();
        var conflicts = details is null ? null : new List<StudentConflictDetail>();
        var occupiedByStudent = occupied.GroupBy(x => x.StudentId).ToDictionary(x => x.Key, x => x.ToArray());
        void Add(Guid studentId, StudentConflictSession a, StudentConflictSession b)
        {
            if (a.SessionId == b.SessionId || a.StartAt >= b.EndAt || a.EndAt <= b.StartAt) return;
            var first = a.SessionId.CompareTo(b.SessionId) < 0 ? a : b;
            var second = ReferenceEquals(first, a) ? b : a;
            if (pairs.Add((studentId, first.SessionId, second.SessionId)))
                conflicts?.Add(new StudentConflictDetail(studentId, first, second,
                    a.StartAt > b.StartAt ? a.StartAt : b.StartAt, a.EndAt < b.EndAt ? a.EndAt : b.EndAt));
        }

        foreach (var studentId in studentIds)
        {
            var studentsSessions = proposed.Where(x => byClass[x.ClassId].Contains(studentId))
                .OrderBy(x => x.StartAt).ToArray();
            for (var i = 0; i < studentsSessions.Length; i++)
            {
                var candidate = studentsSessions[i];
                if (occupiedByStudent.TryGetValue(studentId, out var existing))
                    foreach (var other in existing.Where(x => x.StartAt < candidate.EndAt && x.EndAt > candidate.StartAt))
                        Add(studentId, candidate, new StudentConflictSession(other.Id, other.ClassId, other.StartAt, other.EndAt));
                for (var j = i + 1; j < studentsSessions.Length && studentsSessions[j].StartAt < candidate.EndAt; j++)
                    Add(studentId, candidate, studentsSessions[j]);
            }
        }
        var page = details is null ? null : new PagedResult<StudentConflictDetail>(
            conflicts!.OrderBy(x => x.StudentId).ThenBy(x => x.OverlapStart).ThenBy(x => x.First.SessionId)
                .Skip((details.Page - 1) * details.PageSize).Take(details.PageSize).ToArray(),
            details.Page, details.PageSize, pairs.Count);
        return new StudentConflictPreview(pairs.Select(x => x.StudentId).Distinct().Count(), pairs.Count, page);
    }

    private static StudentConflictPreview Empty(ConflictDetailsRequest? details) =>
        new(0, 0, details is null ? null : new PagedResult<StudentConflictDetail>([], details.Page, details.PageSize, 0));
}
