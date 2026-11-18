using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed class StudentConflictReadService(ClassroomDbContext db, IStudentConflictDetector detector)
    : IStudentConflictReadService
{
    public async Task<StudentConflictPreview> GetMineAsync(Guid studentId, DateTime fromUtc, DateTime toUtc,
        ConflictDetailsRequest? details, CancellationToken ct)
    {
        if (studentId == Guid.Empty || fromUtc.Kind != DateTimeKind.Utc || toUtc.Kind != DateTimeKind.Utc ||
            fromUtc >= toUtc || (toUtc - fromUtc).TotalDays > 366)
            throw new ClassRuleException("invalid_filter", "Valid UTC dates within 366 days are required.");
        var sessions = await (from session in db.ClassSessions.AsNoTracking()
                              join enrollment in db.ClassEnrollments.AsNoTracking() on session.ClassId equals enrollment.ClassId
                              join classroom in db.Classes.AsNoTracking() on session.ClassId equals classroom.Id
                              where enrollment.StudentId == studentId && enrollment.Status == EnrollmentStatus.Active &&
                                    enrollment.WithdrawnAt == null && classroom.Status != ClassStatus.Deleted &&
                                    session.Status != SessionStatus.Cancelled && session.StartAt < toUtc && session.EndAt > fromUtc
                              select new StudentConflictSession(session.Id, session.ClassId, session.StartAt, session.EndAt))
            .ToListAsync(ct);
        return await detector.PreviewAsync(sessions, [studentId], details, ct);
    }
}
