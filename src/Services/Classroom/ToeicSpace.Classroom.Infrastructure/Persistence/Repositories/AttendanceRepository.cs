using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.DTOs.Attendance;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Application.Services;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Persistence.Repositories;

public sealed class AttendanceRepository(
    ClassroomDbContext db,
    StudentEligibilityService eligibility,
    TimeProvider clock) : IAttendanceRepository
{
    private async Task<ClassSession> Authorize(Guid id, Guid actor, string role, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ??
                      throw new ClassRuleException("session_not_found", "Session not found.", 404);
        if (role != "Admin" && (role != "Teacher" ||
                                !await db.ClassInstructors.AnyAsync(
                                    x => x.ClassId == session.ClassId && x.TeacherId == actor && x.UnassignedAt == null,
                                    ct)))
            throw new ClassRuleException("forbidden_class", "No access to attendance.", 403);
        return session;
    }

    private IQueryable<StudentReadModel> Eligible(Guid cls) => db.Students.AsNoTracking().Where(s =>
        s.Role == "User" && s.Status == "Active" && s.IsEligible && s.IsVerified && db.ClassEnrollments.Any(e =>
            e.ClassId == cls && e.StudentId == s.StudentId && e.Status == EnrollmentStatus.Active &&
            e.WithdrawnAt == null));

    public async Task<object> ReadAsync(Guid id, Guid actor, string role, string kind, string? search,
        CancellationToken ct)
    {
        var session = await Authorize(id, actor, role, ct);
        if (kind == "audit")
        {
            if (role != "Admin") throw new ClassRuleException("forbidden_class", "Admin required.", 403);
            return await db.Set<AttendanceAudit>().AsNoTracking().Where(x => x.ClassSessionId == id)
                .OrderBy(x => x.ModifiedAt).ToListAsync(ct);
        }

        var records = await db.Set<AttendanceRecord>().AsNoTracking().Where(x => x.ClassSessionId == id)
            .ToListAsync(ct);
        if (kind == "records") return records;
        var students = Eligible(session.ClassId);
        if (!string.IsNullOrWhiteSpace(search))
            students = students.Where(x => x.Name.Contains(search) || x.Email.Contains(search));
        return (await students.OrderBy(x => x.Name).ToListAsync(ct)).Select(s => new
        {
            s.StudentId, s.Name, s.Email,
            Status = records.FirstOrDefault(r => r.StudentId == s.StudentId)?.Status.ToString() ?? "NotRecorded",
            Record = records.FirstOrDefault(r => r.StudentId == s.StudentId)
        }).ToArray();
    }

    public async Task WriteAsync(Guid id, Guid actor, string role, IReadOnlyList<AttendanceInputDto> items, Guid? remove,
        CancellationToken ct)
    {
        await using var tx = db.Database.IsRelational() ? await db.Database.BeginTransactionAsync(ct) : null;
        if (db.Database.IsRelational())
            await db.ClassSessions.FromSqlInterpolated($"SELECT * FROM ClassSessions WHERE Id = {id} FOR UPDATE")
                .LoadAsync(ct);
        var session = await Authorize(id, actor, role, ct);
        if (session.Status == SessionStatus.Cancelled)
            throw new ClassRuleException("cancelled_session", "Cancelled sessions cannot have attendance changes.",
                409);
        if (items.Count > 100 || items.Select(x => x.StudentId).Distinct().Count() != items.Count)
            throw new ClassRuleException("invalid_attendance", "Select at most 100 distinct students.");
        foreach (var item in items)
        {
            if (!Enum.IsDefined(item.Status) || item.Note?.Length > 1000 || item.StudentId == Guid.Empty)
                throw new ClassRuleException("invalid_attendance", "Invalid status, student or note.");
            await eligibility.EnsureEligibleAsync(item.StudentId, ct);
            if (!await Eligible(session.ClassId).AnyAsync(x => x.StudentId == item.StudentId, ct))
                throw new ClassRuleException("student_not_enrolled", "Student must be actively enrolled.", 409);
        }

        var selected = items.Select(x => x.StudentId).ToArray();
        var existing = await db.Set<AttendanceRecord>()
            .Where(x => x.ClassSessionId == id && (selected.Contains(x.StudentId) || x.StudentId == remove))
            .ToListAsync(ct);
        var now = clock.GetUtcNow().UtcDateTime;
        foreach (var item in items)
        {
            var record = existing.SingleOrDefault(x => x.StudentId == item.StudentId);
            var previous = record?.Status;
            if (record is not null && record.Status == item.Status && record.Note == item.Note) continue;
            var action = record is null ? "Created" : "Updated";
            if (record is null)
            {
                record = new AttendanceRecord
                    { ClassSessionId = id, StudentId = item.StudentId, RecordedBy = actor, RecordedAt = now };
                db.Add(record);
            }

            record.Status = item.Status;
            record.Note = item.Note;
            record.UpdatedAt = now;
            db.Add(new AttendanceAudit
            {
                ClassSessionId = id, StudentId = item.StudentId, PreviousStatus = previous, NewStatus = item.Status,
                Action = action, ModifiedBy = actor, ModifiedAt = now, Note = item.Note
            });
        }

        if (remove is { } student && existing.SingleOrDefault(x => x.StudentId == student) is { } removed)
        {
            db.Remove(removed);
            db.Add(new AttendanceAudit
            {
                ClassSessionId = id, StudentId = student, PreviousStatus = removed.Status, Action = "Removed",
                ModifiedBy = actor, ModifiedAt = now, Note = removed.Note
            });
        }

        await db.SaveChangesAsync(ct);
        if (tx is not null) await tx.CommitAsync(ct);
    }

    public async Task<object> HistoryAsync(Guid student, CancellationToken ct) => await db.Set<AttendanceRecord>()
        .AsNoTracking().Where(x => x.StudentId == student).OrderByDescending(x => x.RecordedAt).ToListAsync(ct);
}