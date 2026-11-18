using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed partial class ClassSchedulingService
{
    private static void Copy(ClassSchedule s, ScheduleInput input, DateTime now)
    {
        s.DaysOfWeek = input.DaysOfWeek.ToList();
        s.StartTime = input.StartTime;
        s.EndTime = input.EndTime;
        s.TeacherId = input.TeacherId;
        s.Mode = input.Mode;
        s.TimeZoneId = input.TimeZoneId;
        s.EffectiveFrom = input.EffectiveFrom;
        s.EffectiveTo = input.EffectiveTo;
        s.ExcludeDates = input.ExcludeDates?.ToList() ?? [];
        s.UpdatedAt = now;
    }

    private static object ScheduleSnapshot(ClassSchedule s) => new
    {
        s.DaysOfWeek, s.StartTime, s.EndTime, s.TeacherId, s.Mode, s.TimeZoneId,
        s.EffectiveFrom, s.EffectiveTo, s.ExcludeDates, s.Version
    };

    private void AuditSchedule(ClassSchedule schedule, string action, Guid actor, object? before)
    {
        db.ClassScheduleAudits.Add(new ClassScheduleAudit
        {
            ClassScheduleId = schedule.Id, ActorId = actor, ChangedAt = Now, Action = action,
            PreviousValue = before is null ? null : JsonSerializer.Serialize(before),
            NewValue = JsonSerializer.Serialize(ScheduleSnapshot(schedule))
        });
    }

    public async Task<IReadOnlyList<ScheduleAuditView>> ScheduleAuditAsync(Guid id, CancellationToken ct)
    {
        if (!await db.ClassSchedules.AnyAsync(x => x.Id == id, ct))
            throw new ClassRuleException("schedule_not_found", "Schedule not found.", 404);
        return await db.ClassScheduleAudits.AsNoTracking().Where(x => x.ClassScheduleId == id)
            .OrderBy(x => x.ChangedAt).Select(x => new ScheduleAuditView(x.Id, x.ClassScheduleId,
                x.ActorId, x.ChangedAt, x.Action, x.PreviousValue, x.NewValue)).ToListAsync(ct);
    }

    public async Task<ClassSchedule> CreateScheduleAsync(ScheduleInput input, Guid actor, CancellationToken ct)
    {
        ValidateSchedule(input);
        await using var tx = await BeginAsync(ct);
        var cls = await LockClassAsync(input.ClassId, ct);
        await ValidateScheduleForClassAsync(input, cls, ct);
        var now = Now;
        var schedule = new ClassSchedule { ClassId = cls.Id, CreatedAt = now };
        Copy(schedule, input, now);
        if (input.Mode == SessionMode.Offline)
            await ValidateSessionAsync(Generated(schedule, cls, input.EffectiveFrom, now), cls, ct);
        db.ClassSchedules.Add(schedule);
        AuditSchedule(schedule, "Created", actor, null);
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        return schedule;
    }

    public async Task<ClassSchedule> GetScheduleAsync(Guid id, CancellationToken ct) =>
        await db.ClassSchedules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ClassRuleException("schedule_not_found", "Schedule not found.", 404);

    public async Task<IReadOnlyList<ClassSchedule>> SchedulesAsync(Guid classId, CancellationToken ct)
    {
        if (!await db.Classes.AnyAsync(x => x.Id == classId && x.Status != ClassStatus.Deleted, ct))
            throw ClassRuleException.NotFound();
        return await db.ClassSchedules.AsNoTracking().Where(x => x.ClassId == classId)
            .OrderBy(x => x.EffectiveFrom).ThenBy(x => x.StartTime).ToListAsync(ct);
    }

    public async Task<SessionOperationResult> GenerateAsync(Guid classId, GenerateSessionsInput input, Guid actor, CancellationToken ct)
    {
        CheckRange(input.From, input.To);
        await using var tx = await BeginAsync(ct);
        var cls = await LockClassAsync(classId, ct);
        if (!cls.CanEdit()) throw ClassRuleException.InvalidState();
        var schedules = await db.ClassSchedules.AsNoTracking().Where(x => x.ClassId == classId).ToListAsync(ct);
        var ids = schedules.Select(x => x.Id).ToArray();
        var existing = await db.ClassSessions.AsNoTracking().Where(x => x.ClassScheduleId != null &&
            ids.Contains(x.ClassScheduleId.Value) && x.OriginalLocalDate >= input.From &&
            x.OriginalLocalDate <= input.To).Select(x => new { x.ClassScheduleId, x.OriginalLocalDate }).ToListAsync(ct);
        var skipped = new List<SessionSkip>();
        var candidates = new List<ClassSession>();
        var excluded = (input.ExcludeDates ?? []).ToHashSet();
        foreach (var schedule in schedules)
            foreach (var date in Dates(schedule, input.From, input.To))
            {
                if (excluded.Contains(date)) continue;
                if (existing.Any(x => x.ClassScheduleId == schedule.Id && x.OriginalLocalDate == date))
                {
                    skipped.Add(new SessionSkip(Guid.Empty, $"Existing session (including cancelled): {schedule.Id}/{date}"));
                    continue;
                }
                var session = Generated(schedule, cls, date, Now);
                if (session.StartAt <= Now) continue;
                candidates.Add(session);
            }
        await LockResourcesAsync(candidates, ct);
        foreach (var session in candidates) await ValidateSessionAsync(session, cls, ct);
        await CheckConflictsAsync(candidates, new HashSet<Guid>(), ct);
        db.ClassSessions.AddRange(candidates);
        foreach (var session in candidates) Audit(session, "Generated", actor, null, null, Now);
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        foreach (var session in candidates.Where(x => x.Mode == SessionMode.Online)) QueueMeet(session);
        return await ResultAsync(candidates, skipped, ct);
    }

    private static void CheckRange(DateOnly from, DateOnly to)
    {
        if (from > to || to.DayNumber - from.DayNumber > 366)
            throw new ClassRuleException("invalid_date_range", "Select a valid range of at most 366 days.");
    }

    public async Task<(ClassSchedule Schedule, SessionOperationResult Sessions)> UpdateScheduleAsync(Guid id,
        ScheduleInput input, int expectedVersion, Guid actor, CancellationToken ct)
    {
        ValidateSchedule(input);
        await using var tx = await BeginAsync(ct);
        var cls = await LockClassAsync(input.ClassId, ct);
        await ValidateScheduleForClassAsync(input, cls, ct);
        var schedule = (db.Database.IsRelational()
            ? await db.ClassSchedules.FromSqlInterpolated($"SELECT * FROM ClassSchedules WHERE Id = {id} FOR UPDATE")
                .SingleOrDefaultAsync(ct)
            : await db.ClassSchedules.SingleOrDefaultAsync(x => x.Id == id, ct))
            ?? throw new ClassRuleException("schedule_not_found", "Schedule not found.", 404);
        if (schedule.ClassId != cls.Id || schedule.Version != expectedVersion) throw Stale();
        var future = await db.ClassSessions.Where(x => x.ClassScheduleId == id)
            .OrderBy(x => x.OriginalLocalDate).ToListAsync(ct);
        var all = future.Select(x => new SessionSkip(x.Id, x.Status != SessionStatus.Scheduled
            ? "Terminal session" : x.StartAt <= Now ? "Already started" : ""))
            .Where(x => x.Reason.Length != 0).ToList();
        var updatable = future.Where(x => x.Status == SessionStatus.Scheduled && x.StartAt > Now).ToArray();
        var horizon = updatable.Select(x => x.OriginalLocalDate).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        var beforeSchedule = ScheduleSnapshot(schedule);
        Copy(schedule, input, Now);
        schedule.Version++;
        AuditSchedule(schedule, "Updated", actor, beforeSchedule);
        var desired = horizon.Length == 0 ? new HashSet<DateOnly>() :
            Dates(schedule, horizon.Min(), horizon.Max()).ToHashSet();
        var candidates = new List<ClassSession>();
        var cancelled = new List<ClassSession>();
        foreach (var session in updatable)
        {
            var old = Snapshot(session);
            if (!session.OriginalLocalDate.HasValue || !desired.Contains(session.OriginalLocalDate.Value))
            {
                if (session.TimeOverridden || session.RoomOverridden || session.TeacherOverridden || session.ModeOverridden)
                { all.Add(new SessionSkip(session.Id, "Overrides preserved outside new recurrence")); continue; }
                session.Status = SessionStatus.Cancelled;
                session.CancellationReason = "Recurring schedule changed";
                session.UpdatedAt = Now;
                session.Version++;
                Audit(session, "CancelledBySchedule", actor, session.CancellationReason, old, Now);
                cancelled.Add(session);
                continue;
            }
            var generated = Generated(schedule, cls, session.OriginalLocalDate.Value, Now);
            if (!session.ModeOverridden && session.Mode != generated.Mode && session.RoomOverridden)
            { all.Add(new SessionSkip(session.Id, "Room override conflicts with mode change")); continue; }
            if (!session.TeacherOverridden) session.TeacherId = generated.TeacherId;
            if (!session.TimeOverridden) { session.StartAt = generated.StartAt; session.EndAt = generated.EndAt; }
            if (!session.ModeOverridden) session.Mode = generated.Mode;
            if (session.Mode == SessionMode.Online) session.RoomId = null;
            else if (!session.RoomOverridden) session.RoomId = cls.DefaultRoomId;
            session.UpdatedAt = Now;
            session.Version++;
            Audit(session, "ScheduleUpdated", actor, null, old, Now);
            candidates.Add(session);
        }
        if (horizon.Length > 0)
        {
            var existingDates = future.Where(x => x.OriginalLocalDate.HasValue)
                .Select(x => x.OriginalLocalDate!.Value).ToHashSet();
            foreach (var date in desired.Except(existingDates))
            {
                var session = Generated(schedule, cls, date, Now);
                if (session.StartAt <= Now) continue;
                candidates.Add(session);
                Audit(session, "Generated", actor, null, null, Now);
                db.ClassSessions.Add(session);
            }
        }
        await LockResourcesAsync(updatable.Concat(candidates), ct);
        foreach (var session in candidates) await ValidateSessionAsync(session, cls, ct);
        await CheckConflictsAsync(candidates, updatable.Select(x => x.Id).ToHashSet(), ct);
        await db.SaveChangesAsync(ct);
        await CommitAsync(tx, ct);
        foreach (var session in candidates.Concat(cancelled)) QueueMeet(session);
        return (schedule, await ResultAsync(candidates.Concat(cancelled), all, ct));
    }
}
