using ToeicSpace.Classroom.Application.DTOs.Attendance;

namespace ToeicSpace.Classroom.Application.Interfaces.Repositories;

public interface IAttendanceRepository
{
    Task<object> ReadAsync(Guid session, Guid actor, string role, string kind, string? search, CancellationToken ct);

    Task WriteAsync(Guid session, Guid actor, string role, IReadOnlyList<AttendanceInputDto> items, Guid? remove,
        CancellationToken ct);

    Task<object> HistoryAsync(Guid student, CancellationToken ct);
}