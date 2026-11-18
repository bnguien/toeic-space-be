using ToeicSpace.Classroom.Application.DTOs.Attendance;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.BulkRecordAttendance;

public sealed record BulkRecordAttendanceCommand(
    Guid Session,
    Guid Actor,
    string Role,
    IReadOnlyList<AttendanceInputDto> Items) : IRequest;