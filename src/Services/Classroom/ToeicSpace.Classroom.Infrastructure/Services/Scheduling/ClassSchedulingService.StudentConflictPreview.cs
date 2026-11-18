using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed partial class ClassSchedulingService
{
    private static StudentConflictSession Proposed(ClassSession session) =>
        new(session.Id, session.ClassId, session.StartAt, session.EndAt);

    private static StudentConflictPreview NoConflicts(ConflictDetailsRequest? details)
    {
        if (details is { } paging && (paging.Page < 1 || paging.PageSize is < 1 or > 100 ||
            paging.Page > int.MaxValue / paging.PageSize))
            throw new ClassRuleException("invalid_pagination", "Valid pagination is required.");
        return new StudentConflictPreview(0, 0, details is null ? null : new([], details.Page, details.PageSize, 0));
    }

    private async Task<Class> PreviewClassAsync(Guid id, CancellationToken ct) =>
        await db.Classes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.Status != ClassStatus.Deleted, ct)
        ?? throw ClassRuleException.NotFound();

    private async Task ValidatePreviewCandidatesAsync(IReadOnlyList<ClassSession> candidates, Class cls, CancellationToken ct)
    {
        foreach (var session in candidates)
            if (session.StartAt.Kind != DateTimeKind.Utc || session.EndAt.Kind != DateTimeKind.Utc ||
                session.StartAt >= session.EndAt || string.IsNullOrWhiteSpace(session.Title) ||
                session.Title.Length > 200 || session.Description?.Length > 2000 || session.Notes?.Length > 2000)
                throw new ClassRuleException("invalid_session", "Invalid session details or UTC interval.");
        // Resource eligibility and date-bound checks need only the earliest/latest occurrence
        // for each resource combination; do not query teacher and room once per generated week.
        foreach (var group in candidates.GroupBy(x => (x.TeacherId, x.RoomId, x.Mode)))
        {
            var ordered = group.OrderBy(x => x.StartAt).ToArray();
            await ValidateSessionAsync(ordered[0], cls, ct);
            if (ordered.Length > 1) await ValidateSessionAsync(ordered[^1], cls, ct);
        }
    }

    public async Task<StudentConflictPreview> CreateSessionAsync(SessionInput input, Guid actor, bool admin,
        ConflictDetailsRequest? details, CancellationToken ct)
    {
        var cls = await PreviewClassAsync(input.ClassId, ct);
        if (!cls.CanEdit() || !admin && cls.Status != ClassStatus.Active) throw ClassRuleException.InvalidState();
        if (!admin && !await db.ClassInstructors.AnyAsync(x => x.ClassId == cls.Id && x.TeacherId == actor &&
                x.UnassignedAt == null, ct))
            throw new ClassRuleException("forbidden_class", "No access to this class.", 403);
        if (!admin && input.TeacherId != actor)
            throw new ClassRuleException("teacher_change_forbidden", "Teachers may create only their own sessions.", 403);
        var session = new ClassSession
        {
            ClassId = cls.Id, TeacherId = input.TeacherId, Title = input.Title?.Trim() ?? "",
            Description = input.Description, SessionType = input.SessionType, StartAt = input.StartAt,
            EndAt = input.EndAt, Mode = input.Mode,
            RoomId = input.Mode == SessionMode.Offline ? input.RoomId ?? cls.DefaultRoomId : input.RoomId,
            Notes = input.Notes
        };
        await ValidateSessionAsync(session, cls, ct);
        await CheckConflictsAsync([session], new HashSet<Guid>(), ct);
        return await StudentConflicts.PreviewAsync([Proposed(session)], null, details, ct);
    }

    public async Task<StudentConflictPreview> UpdateSessionAsync(Guid id, SessionEdit input, Guid actor, bool admin,
        ConflictDetailsRequest? details, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Length > 500)
            throw new ClassRuleException("reason_required", "A change reason of at most 500 characters is required.");
        var current = await db.ClassSessions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ClassRuleException("session_not_found", "Session not found.", 404);
        var cls = await PreviewClassAsync(current.ClassId, ct);
        if (!cls.CanEdit() || !admin && cls.Status != ClassStatus.Active) throw ClassRuleException.InvalidState();
        if (!admin && !await db.ClassInstructors.AnyAsync(x => x.ClassId == cls.Id && x.TeacherId == actor &&
                x.UnassignedAt == null, ct))
            throw new ClassRuleException("forbidden_class", "No access to this class.", 403);
        if (current.Status != SessionStatus.Scheduled) throw ClassRuleException.InvalidState();
        if (current.Version != input.ExpectedVersion) throw Stale();
        if (!admin && (current.TeacherId != actor || input.TeacherId != actor))
            throw new ClassRuleException("teacher_change_forbidden", "Only Admin can change a session teacher.", 403);
        var session = new ClassSession
        {
            Id = id, ClassId = cls.Id, TeacherId = input.TeacherId, Title = input.Title?.Trim() ?? "",
            Description = input.Description, SessionType = current.SessionType, StartAt = input.StartAt,
            EndAt = input.EndAt, Mode = input.Mode,
            RoomId = input.Mode == SessionMode.Offline ? input.RoomId ?? cls.DefaultRoomId : input.RoomId,
            Notes = input.Notes
        };
        await ValidateSessionAsync(session, cls, ct);
        await CheckConflictsAsync([session], new HashSet<Guid> { id }, ct);
        if (current.StartAt == session.StartAt && current.EndAt == session.EndAt) return NoConflicts(details);
        return await StudentConflicts.PreviewAsync([Proposed(session)], null, details, ct);
    }

    public async Task<StudentConflictPreview> CreateScheduleAsync(ScheduleInput input,
        ConflictDetailsRequest? details, CancellationToken ct)
    {
        ValidateSchedule(input);
        var cls = await PreviewClassAsync(input.ClassId, ct);
        await ValidateScheduleForClassAsync(input, cls, ct);
        var schedule = new ClassSchedule { ClassId = cls.Id };
        Copy(schedule, input, Now);
        var candidates = Dates(schedule, input.EffectiveFrom, input.EffectiveTo)
            .Select(date => Generated(schedule, cls, date, Now)).Where(x => x.StartAt > Now).ToArray();
        if (candidates.Length == 0) return NoConflicts(details);
        await ValidatePreviewCandidatesAsync(candidates, cls, ct);
        await CheckConflictsAsync(candidates, new HashSet<Guid>(), ct);
        return await StudentConflicts.PreviewAsync(candidates.Select(Proposed).ToArray(), null, details, ct);
    }

    public async Task<StudentConflictPreview> GenerateSessionsAsync(Guid classId, GenerateSessionsInput input,
        ConflictDetailsRequest? details, CancellationToken ct)
    {
        CheckRange(input.From, input.To);
        var cls = await PreviewClassAsync(classId, ct);
        if (!cls.CanEdit()) throw ClassRuleException.InvalidState();
        var schedules = await db.ClassSchedules.AsNoTracking().Where(x => x.ClassId == classId).ToListAsync(ct);
        var ids = schedules.Select(x => x.Id).ToArray();
        var existing = await db.ClassSessions.AsNoTracking().Where(x => x.ClassScheduleId != null &&
            ids.Contains(x.ClassScheduleId.Value) && x.OriginalLocalDate >= input.From &&
            x.OriginalLocalDate <= input.To).Select(x => new { x.ClassScheduleId, x.OriginalLocalDate }).ToListAsync(ct);
        var dates = existing.Select(x => (x.ClassScheduleId, x.OriginalLocalDate)).ToHashSet();
        var excluded = (input.ExcludeDates ?? []).ToHashSet();
        var candidates = schedules.SelectMany(schedule => Dates(schedule, input.From, input.To)
            .Where(date => !excluded.Contains(date) && !dates.Contains((schedule.Id, (DateOnly?)date)))
            .Select(date => Generated(schedule, cls, date, Now)))
            .Where(x => x.StartAt > Now).ToArray();
        await ValidatePreviewCandidatesAsync(candidates, cls, ct);
        await CheckConflictsAsync(candidates, new HashSet<Guid>(), ct);
        return await StudentConflicts.PreviewAsync(candidates.Select(Proposed).ToArray(), null, details, ct);
    }

    public async Task<StudentConflictPreview> UpdateScheduleAsync(Guid id, ScheduleInput input, int expectedVersion,
        ConflictDetailsRequest? details, CancellationToken ct)
    {
        ValidateSchedule(input);
        var cls = await PreviewClassAsync(input.ClassId, ct);
        await ValidateScheduleForClassAsync(input, cls, ct);
        var old = await db.ClassSchedules.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new ClassRuleException("schedule_not_found", "Schedule not found.", 404);
        if (old.ClassId != cls.Id || old.Version != expectedVersion) throw Stale();
        var schedule = new ClassSchedule { Id = id, ClassId = cls.Id };
        Copy(schedule, input, Now);
        var existing = await db.ClassSessions.AsNoTracking().Where(x => x.ClassScheduleId == id)
            .OrderBy(x => x.OriginalLocalDate).ToListAsync(ct);
        var updatable = existing.Where(x => x.Status == SessionStatus.Scheduled && x.StartAt > Now).ToArray();
        var horizon = updatable.Where(x => x.OriginalLocalDate.HasValue).Select(x => x.OriginalLocalDate!.Value).ToArray();
        var desired = horizon.Length == 0 ? new HashSet<DateOnly>() :
            Dates(schedule, horizon.Min(), horizon.Max()).ToHashSet();
        var candidates = new List<ClassSession>();
        var timeChanges = new List<ClassSession>();
        foreach (var session in updatable)
        {
            if (!session.OriginalLocalDate.HasValue || !desired.Contains(session.OriginalLocalDate.Value)) continue;
            var generated = Generated(schedule, cls, session.OriginalLocalDate.Value, Now);
            if (!session.ModeOverridden && session.Mode != generated.Mode && session.RoomOverridden) continue;
            var changed = new ClassSession
            {
                Id = session.Id, ClassId = cls.Id, TeacherId = session.TeacherOverridden ? session.TeacherId : generated.TeacherId,
                StartAt = session.TimeOverridden ? session.StartAt : generated.StartAt,
                EndAt = session.TimeOverridden ? session.EndAt : generated.EndAt,
                Mode = session.ModeOverridden ? session.Mode : generated.Mode,
                RoomId = session.ModeOverridden ? session.RoomId : generated.RoomId,
                Title = session.Title, Description = session.Description, Notes = session.Notes,
                SessionType = session.SessionType
            };
            if (changed.Mode == SessionMode.Online) changed.RoomId = null;
            else if (!session.RoomOverridden) changed.RoomId = cls.DefaultRoomId;
            candidates.Add(changed);
            if (changed.StartAt != session.StartAt || changed.EndAt != session.EndAt) timeChanges.Add(changed);
        }
        var existingDates = existing.Where(x => x.OriginalLocalDate.HasValue)
            .Select(x => x.OriginalLocalDate!.Value).ToHashSet();
        if (horizon.Length > 0)
        {
            foreach (var date in desired.Except(existingDates))
            {
                var session = Generated(schedule, cls, date, Now);
                if (session.StartAt <= Now) continue;
                candidates.Add(session);
                timeChanges.Add(session);
            }
        }
        else
        {
            // No sessions will be changed by this save, but expose potential future occurrences
            // so Admin can inspect the revised recurrence before an explicit generate operation.
            foreach (var date in Dates(schedule, schedule.EffectiveFrom, schedule.EffectiveTo).Except(existingDates))
            {
                var session = Generated(schedule, cls, date, Now);
                if (session.StartAt <= Now) continue;
                candidates.Add(session);
                timeChanges.Add(session);
            }
        }
        await ValidatePreviewCandidatesAsync(candidates, cls, ct);
        await CheckConflictsAsync(candidates, updatable.Select(x => x.Id).ToHashSet(), ct);
        return await StudentConflicts.PreviewAsync(timeChanges.Select(Proposed).ToArray(), null, details, ct);
    }
}
