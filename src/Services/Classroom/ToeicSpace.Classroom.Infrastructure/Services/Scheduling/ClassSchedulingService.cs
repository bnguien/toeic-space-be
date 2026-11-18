using System.Text.Json;
using System.Data;
using Hangfire;
using Microsoft.Extensions.Logging;
using ToeicSpace.Classroom.Infrastructure.Services.GoogleMeet;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;
using ToeicSpace.Classroom.Infrastructure.Persistence;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed partial class ClassSchedulingService(ClassroomDbContext db, TimeProvider clock, TimeZoneInfo classZone,
    IBackgroundJobClient? jobs = null, ILogger<ClassSchedulingService>? logger = null,
    IStudentConflictDetector? studentConflicts = null)
    : IClassSchedulingService, IStudentConflictPreviewService
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private IStudentConflictDetector StudentConflicts => studentConflicts ?? new StudentConflictDetector(db);
    private void QueueMeet(ClassSession session)
    {
        try { jobs?.Enqueue<GoogleMeetProvisioningJob>(x => x.ProvisionAsync(session.Id, CancellationToken.None)); }
        catch (Exception ex)
        {
            // The reconciliation job will enqueue this committed session again.
            logger?.LogWarning(ex, "Google Meet enqueue failed for session {SessionId}", session.Id);
        }
    }
    private async Task<IDbContextTransaction?> BeginAsync(CancellationToken ct) => db.Database.IsRelational()
        ? await db.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, ct) : null;
    private static Task CommitAsync(IDbContextTransaction? tx, CancellationToken ct) =>
        tx is null ? Task.CompletedTask : tx.CommitAsync(ct);
    private static ClassRuleException Conflict() => new("schedule_conflict", "Teacher or room already has an overlapping session.", 409);
    private static ClassRuleException Stale() => new("concurrency_conflict", "Session changed; reload and retry.", 409);
    private static DateTime Utc(DateTime value) => DateTime.SpecifyKind(value, DateTimeKind.Utc);

    private async Task<Class> LockClassAsync(Guid id, CancellationToken ct) =>
        (db.Database.IsRelational()
            ? await db.Classes.FromSqlInterpolated($"SELECT * FROM Classes WHERE Id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct)
            : await db.Classes.SingleOrDefaultAsync(x => x.Id == id, ct)) ?? throw ClassRuleException.NotFound();

    private Task<ClassSession> LockSessionAsync(Guid id, CancellationToken ct) => db.Database.IsRelational()
        ? db.ClassSessions.FromSqlInterpolated($"SELECT * FROM ClassSessions WHERE Id = {id} FOR UPDATE").SingleAsync(ct)
        : db.ClassSessions.SingleAsync(x => x.Id == id, ct);

    // Every write locks its class, then resource rows sorted ordinally. INSERT IGNORE creates
    // an InnoDB lock row even when no sessions exist yet (predicate locks cannot protect gaps).
    private async Task LockResourcesAsync(IEnumerable<ClassSession> sessions, CancellationToken ct)
    {
        if (!db.Database.IsRelational()) return;
        var keys = sessions.SelectMany(s => s.Mode == SessionMode.Offline && s.RoomId.HasValue
                ? new[] { $"T:{s.TeacherId:N}", $"R:{s.RoomId.Value:N}" }
                : new[] { $"T:{s.TeacherId:N}" })
            .Distinct().Order(StringComparer.Ordinal).ToArray();
        foreach (var key in keys)
        {
            await db.Database.ExecuteSqlInterpolatedAsync($"INSERT IGNORE INTO SchedulingResources (`Key`) VALUES ({key})", ct);
            _ = await db.SchedulingResources.FromSqlInterpolated($"SELECT * FROM SchedulingResources WHERE `Key` = {key} FOR UPDATE")
                .AsNoTracking().SingleAsync(ct);
        }
    }

    private async Task CheckConflictsAsync(IReadOnlyList<ClassSession> candidates, IReadOnlySet<Guid> excluded, CancellationToken ct)
    {
        var active = candidates.Where(x => x.Status == SessionStatus.Scheduled).ToArray();
        if (active.Length == 0) return;
        var start = active.Min(x => x.StartAt);
        var end = active.Max(x => x.EndAt);
        // READ COMMITTED + resource rows locked by this transaction: each query observes the
        // last committed schedule without taking broad range/gap locks on unrelated sessions.
        var teacherIds = active.Select(x => x.TeacherId).Distinct().ToArray();
        var roomIds = active.Where(x => x.Mode == SessionMode.Offline && x.RoomId.HasValue)
            .Select(x => x.RoomId!.Value).Distinct().ToArray();
        var existing = await db.ClassSessions.AsNoTracking().Where(x => x.StartAt < end && x.EndAt > start &&
            (teacherIds.Contains(x.TeacherId) || x.RoomId.HasValue && roomIds.Contains(x.RoomId.Value)))
            .ToListAsync(ct);
        foreach (var candidate in active)
        {
            if (existing.Any(x => !excluded.Contains(x.Id) && x.Status != SessionStatus.Cancelled && SchedulingRules.Conflicts(x, candidate)))
                throw Conflict();
            if (active.Any(x => x.Id != candidate.Id && SchedulingRules.Conflicts(x, candidate))) throw Conflict();
        }
    }

    private async Task ValidateSessionAsync(ClassSession session, Class cls, CancellationToken ct)
    {
        if (!cls.CanEdit()) throw ClassRuleException.InvalidState();
        if (session.TeacherId == Guid.Empty || session.StartAt.Kind != DateTimeKind.Utc ||
            session.EndAt.Kind != DateTimeKind.Utc || session.StartAt >= session.EndAt ||
            string.IsNullOrWhiteSpace(session.Title) || session.Title.Length > 200 ||
            session.Description?.Length > 2000 || session.Notes?.Length > 2000 ||
            !Enum.IsDefined(session.Mode) || !Enum.IsDefined(session.SessionType))
            throw new ClassRuleException("invalid_session", "Invalid session details or UTC interval.");
        if (!await db.ClassInstructors.AnyAsync(x => x.ClassId == cls.Id && x.TeacherId == session.TeacherId && x.UnassignedAt == null, ct) ||
            !await db.Teachers.AnyAsync(x => x.TeacherId == session.TeacherId && x.IsEligible, ct))
            throw new ClassRuleException("teacher_not_assigned", "An active assigned teacher is required.", 409);
        if (cls.StartDate is null || cls.EndDate is null)
            throw new ClassRuleException("required_dates", "Class must have start and end dates.", 409);
        var zone = classZone.Id;
        if (session.Mode == SessionMode.Online)
        {
            if (session.RoomId is not null) throw new ClassRuleException("online_room", "Online sessions cannot occupy a room.");
            if (cls.DefaultRoomId is { } defaultRoom)
                zone = await db.Rooms.AsNoTracking().Where(x => x.Id == defaultRoom)
                    .Select(x => x.Center.TimeZoneId).SingleOrDefaultAsync(ct) ?? classZone.Id;
        }
        else
        {
            // CenterId is immutable. Lock Center before Room, matching SetDefaultRoomHandler.
            var centerId = session.RoomId is { } id ? await db.Rooms.AsNoTracking()
                .Where(x => x.Id == id).Select(x => x.CenterId).SingleOrDefaultAsync(ct) : Guid.Empty;
            var center = centerId != Guid.Empty ? await LockCenterAsync(centerId, ct) : null;
            var room = session.RoomId is { } roomId ? await LockRoomAsync(roomId, ct) : null;
            if (room is null || center is null || room.Status != FacilityStatus.Active || center.Status != FacilityStatus.Active)
                throw new ClassRuleException("inactive_room", "An active room and center are required.", 409);
            zone = center.TimeZoneId;
            var enrolled = await db.ClassEnrollments.CountAsync(x => x.ClassId == cls.Id && x.Status == EnrollmentStatus.Active &&
                x.WithdrawnAt == null, ct);
            if (enrolled > room.Capacity) throw new ClassRuleException("room_too_small", "Room capacity is below active enrollment count.", 409);
        }
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(zone);
        var localStart = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(session.StartAt, timeZone));
        var localEnd = DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(session.EndAt.AddTicks(-1), timeZone));
        if (localStart < cls.StartDate || localEnd > cls.EndDate)
            throw new ClassRuleException("outside_class_dates", "Session must fall within the class date range.", 409);
    }

    private Task<Room?> LockRoomAsync(Guid id, CancellationToken ct) => db.Database.IsRelational()
        ? db.Rooms.FromSqlInterpolated($"SELECT * FROM Rooms WHERE Id = {id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct)
        : db.Rooms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    private Task<Center?> LockCenterAsync(Guid id, CancellationToken ct) => db.Database.IsRelational()
        ? db.Centers.FromSqlInterpolated($"SELECT * FROM Centers WHERE Id = {id} FOR UPDATE").AsNoTracking().SingleOrDefaultAsync(ct)
        : db.Centers.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct);

    private static void ValidateSchedule(ScheduleInput input)
    {
        if (input.ClassId == Guid.Empty || input.TeacherId == Guid.Empty || input.DaysOfWeek is null ||
            input.DaysOfWeek.Count == 0 || input.DaysOfWeek.Distinct().Count() != input.DaysOfWeek.Count ||
            input.DaysOfWeek.Any(x => !Enum.IsDefined(x)) || input.StartTime >= input.EndTime ||
            input.EffectiveFrom > input.EffectiveTo || !Enum.IsDefined(input.Mode) ||
            string.IsNullOrWhiteSpace(input.TimeZoneId) || input.TimeZoneId.Length > 100 ||
            input.ExcludeDates is { } dates && dates.Distinct().Count() != dates.Count)
            throw new ClassRuleException("invalid_schedule", "Invalid weekly schedule.");
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(input.TimeZoneId); }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        { throw new ClassRuleException("invalid_time_zone", "Invalid time zone.", 400, ex); }
    }

    private async Task ValidateScheduleForClassAsync(ScheduleInput input, Class cls, CancellationToken ct)
    {
        ValidateSchedule(input);
        if (!cls.CanEdit()) throw ClassRuleException.InvalidState();
        if (cls.StartDate is null || cls.EndDate is null || input.EffectiveFrom < cls.StartDate || input.EffectiveTo > cls.EndDate)
            throw new ClassRuleException("outside_class_dates", "Schedule must fall within class dates.", 409);
        if (!await db.ClassInstructors.AnyAsync(x => x.ClassId == cls.Id && x.TeacherId == input.TeacherId && x.UnassignedAt == null, ct) ||
            !await db.Teachers.AnyAsync(x => x.TeacherId == input.TeacherId && x.IsEligible, ct))
            throw new ClassRuleException("teacher_not_assigned", "Teacher must be active and assigned to class.", 409);
        if (input.Mode == SessionMode.Offline && cls.DefaultRoomId is null)
            throw new ClassRuleException("room_required", "An offline schedule requires a default room.", 409);
    }

    private static IEnumerable<DateOnly> Dates(ClassSchedule schedule, DateOnly from, DateOnly to)
    {
        var start = from > schedule.EffectiveFrom ? from : schedule.EffectiveFrom;
        var end = to < schedule.EffectiveTo ? to : schedule.EffectiveTo;
        for (var date = start; date <= end; date = date.AddDays(1))
        {
            if (schedule.DaysOfWeek.Contains(date.DayOfWeek) && !schedule.ExcludeDates.Contains(date)) yield return date;
            if (date == end) break;
        }
    }

    private static ClassSession Generated(ClassSchedule schedule, Class cls, DateOnly date, DateTime now) => new()
    {
        ClassId = cls.Id, ClassScheduleId = schedule.Id, OriginalLocalDate = date,
        TeacherId = schedule.TeacherId, Title = cls.Name, SessionType = SessionType.Regular,
        StartAt = SchedulingRules.LocalToUtc(date, schedule.StartTime, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId)),
        EndAt = SchedulingRules.LocalToUtc(date, schedule.EndTime, TimeZoneInfo.FindSystemTimeZoneById(schedule.TimeZoneId)),
        Mode = schedule.Mode, RoomId = schedule.Mode == SessionMode.Offline ? cls.DefaultRoomId : null,
        CreatedAt = now, UpdatedAt = now
    };

    private void Audit(ClassSession session, string action, Guid actor, string? reason, object? before, DateTime now)
    {
        db.ClassSessionAudits.Add(new ClassSessionAudit
        {
            ClassSessionId = session.Id, ActorId = actor, ChangedAt = now,
            Action = action, Reason = reason,
            PreviousValue = before is null ? null : JsonSerializer.Serialize(before),
            NewValue = JsonSerializer.Serialize(Snapshot(session))
        });
    }

    private static object Snapshot(ClassSession s) => new
    {
        s.TeacherId, s.StartAt, s.EndAt, s.Mode, s.RoomId, s.Status,
        s.Title, s.Description, s.Notes, s.CancellationReason, s.CompletedAt,
        s.TimeOverridden, s.RoomOverridden, s.TeacherOverridden, s.ModeOverridden
    };

    private async Task<SessionView> ViewAsync(ClassSession s, CancellationToken ct)
    {
        var room = s.RoomId is { } id ? await db.Rooms.AsNoTracking().Include(x => x.Center)
            .SingleOrDefaultAsync(x => x.Id == id, ct) : null;
        return new SessionView(s.Id, s.ClassId, s.ClassScheduleId, s.OriginalLocalDate,
            s.TeacherId, s.Title, s.Description, s.SessionType, Utc(s.StartAt), Utc(s.EndAt),
            s.Mode, s.RoomId, room?.Name, room?.CenterId, room?.Center.Name,
            room?.Center.Address, room?.Center.TimeZoneId, s.Status, s.Notes,
            s.CancellationReason, s.CompletedAt is { } done ? Utc(done) : null,
            s.TimeOverridden, s.RoomOverridden, s.TeacherOverridden, s.ModeOverridden,
            Utc(s.CreatedAt), Utc(s.UpdatedAt), s.Version);
    }

    private async Task<SessionOperationResult> ResultAsync(IEnumerable<ClassSession> affected, IReadOnlyList<SessionSkip> skipped, CancellationToken ct)
    {
        var items = new List<SessionView>();
        foreach (var s in affected) items.Add(await ViewAsync(s, ct));
        return new SessionOperationResult(items, skipped);
    }
}
