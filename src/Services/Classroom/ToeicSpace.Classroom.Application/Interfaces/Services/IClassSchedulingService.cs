using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Interfaces.Services;

public interface IClassSchedulingService
{
    Task<ClassSchedule> CreateScheduleAsync(ScheduleInput input, Guid actor, CancellationToken ct);

    Task<(ClassSchedule Schedule, SessionOperationResult Sessions)> UpdateScheduleAsync(Guid id, ScheduleInput input,
        int expectedVersion, Guid actor, CancellationToken ct);

    Task<ClassSchedule> GetScheduleAsync(Guid id, CancellationToken ct);
    Task<IReadOnlyList<ClassSchedule>> SchedulesAsync(Guid classId, CancellationToken ct);
    Task<IReadOnlyList<ScheduleAuditView>> ScheduleAuditAsync(Guid id, CancellationToken ct);

    Task<SessionOperationResult> GenerateAsync(Guid classId, GenerateSessionsInput input, Guid actor,
        CancellationToken ct);

    Task<SessionOperationResult> CancelRangeAsync(Guid classId, DateTime from, DateTime to,
        string reason, Guid actor, CancellationToken ct);

    Task<SessionView> CreateAsync(SessionInput input, Guid actor, bool admin, CancellationToken ct);
    Task<SessionView> UpdateAsync(Guid id, SessionEdit input, Guid actor, bool admin, CancellationToken ct);
    Task<SessionView> CancelAsync(Guid id, string reason, Guid actor, bool admin, CancellationToken ct);
    Task<SessionView> CompleteAsync(Guid id, Guid actor, bool admin, CancellationToken ct);
    Task<SessionView> GetAsync(Guid id, Guid actor, string role, CancellationToken ct);

    Task<PagedResult<SessionView>> ListAsync(Guid actor, string role, Guid? classId, Guid? teacherId,
        SessionStatus? status, DateTime from, DateTime to, int page, int pageSize, CancellationToken ct);

    Task<IReadOnlyList<SessionAuditView>> AuditAsync(Guid id, CancellationToken ct);
    Task PrepareDefaultRoomChangeAsync(Guid classId, Guid? newRoomId, DateTime now, CancellationToken ct);

    Task ApplyDefaultRoomAsync(Guid classId, Guid? newRoomId, Guid actor, string? reason, DateTime now,
        CancellationToken ct);
}