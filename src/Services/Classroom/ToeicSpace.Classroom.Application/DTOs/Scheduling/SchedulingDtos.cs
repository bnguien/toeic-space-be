using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.DTOs.Scheduling;

public sealed record ScheduleInput(
    Guid ClassId,
    IReadOnlyList<DayOfWeek> DaysOfWeek,
    TimeOnly StartTime,
    TimeOnly EndTime,
    Guid TeacherId,
    SessionMode Mode,
    string TimeZoneId,
    DateOnly EffectiveFrom,
    DateOnly EffectiveTo,
    IReadOnlyList<DateOnly>? ExcludeDates);

public sealed record SessionInput(
    Guid ClassId,
    Guid TeacherId,
    string Title,
    string? Description,
    SessionType SessionType,
    DateTime StartAt,
    DateTime EndAt,
    SessionMode Mode,
    Guid? RoomId,
    string? Notes);

public sealed record SessionEdit(
    Guid TeacherId,
    string Title,
    string? Description,
    DateTime StartAt,
    DateTime EndAt,
    SessionMode Mode,
    Guid? RoomId,
    string? Notes,
    string Reason,
    int ExpectedVersion);

public sealed record SessionView(
    Guid Id,
    Guid ClassId,
    Guid? ClassScheduleId,
    DateOnly? OriginalLocalDate,
    Guid TeacherId,
    string Title,
    string? Description,
    SessionType SessionType,
    DateTime StartAt,
    DateTime EndAt,
    SessionMode Mode,
    Guid? RoomId,
    string? RoomName,
    Guid? CenterId,
    string? CenterName,
    string? CenterAddress,
    string? TimeZoneId,
    SessionStatus Status,
    string? Notes,
    string? CancellationReason,
    DateTime? CompletedAt,
    bool TimeOverridden,
    bool RoomOverridden,
    bool TeacherOverridden,
    bool ModeOverridden,
    DateTime CreatedAt,
    DateTime UpdatedAt,
    int Version);

public sealed record SessionAuditView(
    Guid Id,
    Guid ClassSessionId,
    Guid ActorId,
    DateTime ChangedAt,
    string Action,
    string? Reason,
    string? PreviousValue,
    string? NewValue);

public sealed record ScheduleAuditView(
    Guid Id,
    Guid ClassScheduleId,
    Guid ActorId,
    DateTime ChangedAt,
    string Action,
    string? PreviousValue,
    string NewValue);

public sealed record SessionOperationResult(
    IReadOnlyList<SessionView> Affected,
    IReadOnlyList<SessionSkip> Skipped);

public sealed record SessionSkip(Guid SessionId, string Reason);

public sealed record GenerateSessionsInput(DateOnly From, DateOnly To, IReadOnlyList<DateOnly>? ExcludeDates);