using Microsoft.EntityFrameworkCore;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Infrastructure.Services.Scheduling;

public sealed partial class ClassSchedulingService
{
    // Called after the Class lock and before Center/Room locks in the caller's transaction.
    public async Task PrepareDefaultRoomChangeAsync(Guid classId, Guid? newRoomId, DateTime now, CancellationToken ct)
    {
        var sessions = await db.ClassSessions.AsNoTracking().Where(x => x.ClassId == classId &&
            x.Status == SessionStatus.Scheduled && x.StartAt > now && x.Mode == SessionMode.Offline &&
            !x.RoomOverridden).ToListAsync(ct);
        var replacements = sessions.Select(x => new ClassSession { TeacherId = x.TeacherId,
            RoomId = newRoomId, Mode = SessionMode.Offline }).ToArray();
        await LockResourcesAsync(sessions.Concat(replacements), ct);
    }

    // Called inside SetDefaultRoomHandler's transaction after its Class row lock. Do not
    // commit here: the class assignment and every eligible session/audit must commit together.
    public async Task ApplyDefaultRoomAsync(Guid classId, Guid? newRoomId, Guid actor,
        string? reason, DateTime now, CancellationToken ct)
    {
        var cls = await db.Classes.SingleAsync(x => x.Id == classId, ct);
        var sessions = await db.ClassSessions.Where(x => x.ClassId == classId &&
            x.Status == SessionStatus.Scheduled && x.StartAt > now && x.Mode == SessionMode.Offline &&
            !x.RoomOverridden).ToListAsync(ct);
        if (sessions.Count == 0) return;
        if (newRoomId is null)
            throw new ClassRuleException("room_required", "Change future offline sessions to Online or a different room before clearing the default.", 409);
        var previous = sessions.Select(x => new ClassSession { TeacherId = x.TeacherId, RoomId = x.RoomId,
            Mode = x.Mode, StartAt = x.StartAt, EndAt = x.EndAt }).ToArray();
        var before = sessions.ToDictionary(x => x.Id, Snapshot);
        foreach (var session in sessions) session.RoomId = newRoomId;
        foreach (var session in sessions) await ValidateSessionAsync(session, cls, ct);
        await LockResourcesAsync(previous.Concat(sessions), ct);
        await CheckConflictsAsync(sessions, sessions.Select(x => x.Id).ToHashSet(), ct);
        foreach (var session in sessions)
        {
            session.UpdatedAt = now;
            session.Version++;
            Audit(session, "DefaultRoomChanged", actor, reason, before[session.Id], now);
        }
    }
}
