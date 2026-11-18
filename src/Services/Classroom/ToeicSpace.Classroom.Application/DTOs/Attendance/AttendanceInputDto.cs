using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.DTOs.Attendance;

public sealed record AttendanceInputDto(Guid StudentId, AttendanceStatus Status, string? Note);
