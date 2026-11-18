using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed partial class ClassSchedulingService
{
    private async Task AuthorizeAsync(Guid classId, Guid actor, string role, CancellationToken ct)
    {
        if (role == "Admin") return;
        if (!await db.Classes.AnyAsync(x => x.Id == classId && x.Status != ClassStatus.Deleted, ct))
            throw ClassRuleException.NotFound();
        if (role == "Teacher" && await db.ClassInstructors.AnyAsync(x => x.ClassId == classId &&
            x.TeacherId == actor && x.UnassignedAt == null, ct)) return;
        if (role == "User" && await db.ClassEnrollments.AnyAsync(x => x.ClassId == classId &&
            x.StudentId == actor && x.Status == EnrollmentStatus.Active && x.WithdrawnAt == null, ct)) return;
        throw new ClassRuleException("forbidden_class", "No access to this class.", 403);
    }

    private async Task<Class> LockForWriteAsync(Guid classId, Guid actor, bool admin, CancellationToken ct,
        bool allowCompleted = false)
    {
        var cls = await LockClassAsync(classId, ct);
        if (!cls.CanEdit() && !(allowCompleted && cls.Status == ClassStatus.Completed))
            throw ClassRuleException.InvalidState();
        if (!admin)
        {
            if (cls.Status != ClassStatus.Active && !(allowCompleted && cls.Status == ClassStatus.Completed))
                throw ClassRuleException.InvalidState();
            await AuthorizeAsync(classId, actor, "Teacher", ct);
        }
        return cls;
    }

    public async Task<SessionView> CreateAsync(SessionInput input, Guid actor, bool admin, CancellationToken ct)
    {
        await using var tx = await BeginAsync(ct);
        var cls = await LockForWriteAsync(input.ClassId, actor, admin, ct);
        if (!admin && input.TeacherId != actor)
            throw new ClassRuleException("teacher_change_forbidden", "Teachers may create only their own sessions.", 403);
        var now = Now;
        var session = new ClassSession
        {
            ClassId = cls.Id, TeacherId = input.TeacherId, Title = input.Title?.Trim() ?? "",
            Description = input.Description, SessionType = input.SessionType,
            StartAt = input.StartAt, EndAt = input.EndAt, Mode = input.Mode,
            RoomId = input.Mode == SessionMode.Offline ? input.RoomId ?? cls.DefaultRoomId : input.RoomId,
            Notes = input.Notes, CreatedAt = now, UpdatedAt = now
        };
        await LockResourcesAsync([session], ct);
        await ValidateSessionAsync(session, cls, ct);
        await CheckConflictsAsync([session], new HashSet<Guid>(), ct);
        db.ClassSessions.Add(session);
        Audit(session, "Created", actor, null, null, now);
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        if (session.Mode == SessionMode.Online) QueueMeet(session);
        return await ViewAsync(session, ct);
    }

    public async Task<SessionView> UpdateAsync(Guid id, SessionEdit input, Guid actor, bool admin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 500)
            throw new ClassRuleException("reason_required", "A change reason of at most 500 characters is required.");
        // Initial untracked read identifies the class/resource set. The class row lock serializes
        // every write to its sessions; reload after locking and check the caller's version.
        var lookup = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ClassRuleException("session_not_found", "Session not found.", 404);
        await using var tx = await BeginAsync(ct);
        var cls = await LockForWriteAsync(lookup.ClassId, actor, admin, ct);
        var session = await LockSessionAsync(id, ct);
        if (session.Status != SessionStatus.Scheduled) throw ClassRuleException.InvalidState();
        if (session.Version != input.ExpectedVersion) throw Stale();
        if (!admin && (session.TeacherId != actor || input.TeacherId != actor))
            throw new ClassRuleException("teacher_change_forbidden", "Only Admin can change a session teacher.", 403);
        var before = Snapshot(session);
        var previous = new ClassSession { TeacherId = session.TeacherId, Mode = session.Mode, RoomId = session.RoomId };
        session.TimeOverridden |= session.StartAt != input.StartAt || session.EndAt != input.EndAt;
        session.TeacherOverridden |= session.TeacherId != input.TeacherId;
        session.ModeOverridden |= session.Mode != input.Mode;
        if (input.Mode == SessionMode.Online && input.RoomId is not null)
            throw new ClassRuleException("online_room", "Online sessions cannot occupy a room.");
        var desiredRoom = input.Mode == SessionMode.Online ? null : input.RoomId ?? cls.DefaultRoomId;
        session.RoomOverridden |= input.RoomId is not null && session.RoomId != desiredRoom &&
            input.Mode == SessionMode.Offline;
        session.TeacherId = input.TeacherId;
        session.Title = input.Title?.Trim() ?? "";
        session.Description = input.Description;
        session.StartAt = input.StartAt;
        session.EndAt = input.EndAt;
        session.Mode = input.Mode;
        session.RoomId = desiredRoom;
        session.Notes = input.Notes;
        session.UpdatedAt = Now;
        session.Version++;
        await LockResourcesAsync([previous, session], ct);
        await ValidateSessionAsync(session, cls, ct);
        await CheckConflictsAsync([session], new HashSet<Guid> { id }, ct);
        Audit(session, "Updated", actor, input.Reason.Trim(), before, Now);
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        QueueMeet(session);
        return await ViewAsync(session, ct);
    }

    public async Task<SessionView> CancelAsync(Guid id, string reason, Guid actor, bool admin, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500)
            throw new ClassRuleException("reason_required", "Cancellation requires a reason of at most 500 characters.");
        var lookup = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ClassRuleException("session_not_found", "Session not found.", 404);
        await using var tx = await BeginAsync(ct);
        await LockForWriteAsync(lookup.ClassId, actor, admin, ct, allowCompleted: true);
        var session = await LockSessionAsync(id, ct);
        if (session.Status != SessionStatus.Scheduled) throw ClassRuleException.InvalidState();
        if (session.Version != lookup.Version) throw Stale();
        if (!admin && session.TeacherId != actor)
            throw new ClassRuleException("forbidden_class", "Only the assigned session teacher may cancel it.", 403);
        await LockResourcesAsync([session], ct);
        var before = Snapshot(session);
        session.Status = SessionStatus.Cancelled;
        session.CancellationReason = reason.Trim();
        session.UpdatedAt = Now;
        session.Version++;
        Audit(session, "Cancelled", actor, reason.Trim(), before, Now);
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        QueueMeet(session);
        return await ViewAsync(session, ct);
    }

    public async Task<SessionView> CompleteAsync(Guid id, Guid actor, bool admin, CancellationToken ct)
    {
        var lookup = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ClassRuleException("session_not_found", "Session not found.", 404);
        await using var tx = await BeginAsync(ct);
        await LockForWriteAsync(lookup.ClassId, actor, admin, ct, allowCompleted: true);
        var session = await LockSessionAsync(id, ct);
        if (session.Status != SessionStatus.Scheduled) throw ClassRuleException.InvalidState();
        if (session.Version != lookup.Version) throw Stale();
        if (!admin && session.TeacherId != actor)
            throw new ClassRuleException("forbidden_class", "Only the assigned session teacher may complete it.", 403);
        if (session.EndAt > Now) throw new ClassRuleException("session_not_finished", "Cannot complete before scheduled end.", 409);
        await LockResourcesAsync([session], ct);
        var before = Snapshot(session);
        session.Status = SessionStatus.Completed;
        session.CompletedAt = Now;
        session.UpdatedAt = Now;
        session.Version++;
        Audit(session, "Completed", actor, null, before, Now);
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        QueueMeet(session);
        return await ViewAsync(session, ct);
    }

    public async Task<SessionOperationResult> CancelRangeAsync(Guid classId, DateTime from, DateTime to,
        string reason, Guid actor, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Length > 500 || from >= to || from.Kind != DateTimeKind.Utc ||
            to.Kind != DateTimeKind.Utc || (to - from).TotalDays > 366)
            throw new ClassRuleException("invalid_cancel_range", "Valid UTC range and reason are required.");
        await using var tx = await BeginAsync(ct);
        await LockForWriteAsync(classId, actor, true, ct, allowCompleted: true);
        var sessions = await db.ClassSessions.Where(x => x.ClassId == classId && x.StartAt >= from && x.StartAt < to)
            .OrderBy(x => x.StartAt).ToListAsync(ct);
        var affected = sessions.Where(x => x.Status == SessionStatus.Scheduled).ToArray();
        await LockResourcesAsync(affected, ct);
        foreach (var session in affected)
        {
            var before = Snapshot(session);
            session.Status = SessionStatus.Cancelled;
            session.CancellationReason = reason.Trim();
            session.UpdatedAt = Now;
            session.Version++;
            Audit(session, "BulkCancelled", actor, reason.Trim(), before, Now);
        }
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        foreach (var session in affected) QueueMeet(session);
        return await ResultAsync(affected, sessions.Except(affected).Select(x =>
            new SessionSkip(x.Id, "Terminal session")).ToArray(), ct);
    }

    public async Task<SessionView> GetAsync(Guid id, Guid actor, string role, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ClassRuleException("session_not_found", "Session not found.", 404);
        await AuthorizeAsync(session.ClassId, actor, role, ct);
        return await ViewAsync(session, ct);
    }

    public async Task<PagedResult<SessionView>> ListAsync(Guid actor, string role, Guid? classId, Guid? teacherId,
        SessionStatus? status, DateTime from, DateTime to, int page, int pageSize, CancellationToken ct)
    {
        if (page < 1 || pageSize is < 1 or > 100 || page > int.MaxValue / pageSize || from.Kind != DateTimeKind.Utc ||
            to.Kind != DateTimeKind.Utc || from >= to || (to - from).TotalDays > 366 ||
            status is { } value && !Enum.IsDefined(value))
            throw new ClassRuleException("invalid_filter", "Valid UTC dates and pagination are required.");
        if (classId is { } cls) await AuthorizeAsync(cls, actor, role, ct);
        var query = db.ClassSessions.AsNoTracking().Where(x => x.StartAt < to && x.EndAt > from);
        if (classId is { } id) query = query.Where(x => x.ClassId == id);
        if (role == "Teacher") query = query.Where(x => x.TeacherId == actor && x.ClassId != Guid.Empty &&
            db.ClassInstructors.Any(i => i.ClassId == x.ClassId && i.TeacherId == actor && i.UnassignedAt == null) &&
            db.Classes.Any(c => c.Id == x.ClassId && c.Status != ClassStatus.Deleted));
        else if (role == "User") query = query.Where(x => db.ClassEnrollments.Any(e => e.ClassId == x.ClassId &&
            e.StudentId == actor && e.Status == EnrollmentStatus.Active && e.WithdrawnAt == null) &&
            db.Classes.Any(c => c.Id == x.ClassId && c.Status != ClassStatus.Deleted));
        else if (role != "Admin") throw new ClassRuleException("forbidden_class", "Unsupported role.", 403);
        if (teacherId is { } teacher && role == "Admin") query = query.Where(x => x.TeacherId == teacher);
        if (status is { } filter) query = query.Where(x => x.Status == filter);
        var total = await query.CountAsync(ct);
        var sessions = await query.OrderBy(x => x.StartAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        var items = new List<SessionView>(sessions.Count);
        foreach (var session in sessions) items.Add(await ViewAsync(session, ct));
        return new PagedResult<SessionView>(items, page, pageSize, total);
    }

    public async Task<IReadOnlyList<SessionAuditView>> AuditAsync(Guid id, CancellationToken ct) =>
        await db.ClassSessionAudits.AsNoTracking().Where(x => x.ClassSessionId == id).OrderBy(x => x.ChangedAt)
            .Select(x => new SessionAuditView(x.Id, x.ClassSessionId, x.ActorId, x.ChangedAt,
                x.Action, x.Reason, x.PreviousValue, x.NewValue)).ToListAsync(ct);
}
