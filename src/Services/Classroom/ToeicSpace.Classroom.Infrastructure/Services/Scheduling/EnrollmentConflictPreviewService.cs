using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed class EnrollmentConflictPreviewService(ClassroomDbContext db, IStudentConflictDetector conflicts,
    TimeProvider clock) : IEnrollmentConflictPreviewService
{
    public async Task<StudentConflictPreview> PreviewAsync(Guid classId, IReadOnlyList<Guid> studentIds,
        ConflictDetailsRequest? details, CancellationToken ct)
    {
        if (studentIds is null || studentIds.Count is < 1 or > 1000 || studentIds.Contains(Guid.Empty) ||
            studentIds.Distinct().Count() != studentIds.Count)
            throw new ClassRuleException("invalid_students", "Provide 1 to 1000 distinct student identifiers.");
        var classroom = await db.Classes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == classId &&
            x.Status != ClassStatus.Deleted, ct) ?? throw ClassRuleException.NotFound();
        if (!classroom.CanManageEnrollments()) throw ClassRuleException.InvalidState();
        var students = await db.Students.AsNoTracking().Where(x => studentIds.Contains(x.StudentId)).ToListAsync(ct);
        if (students.Count != studentIds.Count)
            throw new ClassRuleException("student_not_synchronized", "Student data is not synchronized yet. Retry later.", 503);
        if (students.Any(x => !x.IsEligible || !x.IsVerified ||
            !string.Equals(x.Role, "User", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(x.Status, "Active", StringComparison.OrdinalIgnoreCase)))
            throw new ClassRuleException("ineligible_student", "Only active verified students may enroll.");
        if (await db.ClassEnrollments.AnyAsync(x => x.ClassId == classId && studentIds.Contains(x.StudentId) &&
                x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null, ct))
            throw new ClassRuleException("already_enrolled", "Student is already enrolled in this class.", 409);
        var now = clock.GetUtcNow().UtcDateTime;
        var sessions = await db.ClassSessions.AsNoTracking().Where(x => x.ClassId == classId &&
                x.Status != SessionStatus.Cancelled && x.EndAt > now)
            .Select(x => new StudentConflictSession(x.Id, x.ClassId, x.StartAt, x.EndAt)).ToListAsync(ct);
        return await conflicts.PreviewAsync(sessions, studentIds, details, ct);
    }
}
