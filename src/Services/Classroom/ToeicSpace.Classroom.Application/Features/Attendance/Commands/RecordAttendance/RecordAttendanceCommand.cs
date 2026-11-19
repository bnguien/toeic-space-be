using ToeicSpace.Classroom.Application.DTOs.Attendance;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.RecordAttendance;

public sealed record RecordAttendanceCommand(Guid Session, Guid Actor, string Role, AttendanceInputDto Entry) : IRequest;