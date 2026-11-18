using ToeicSpace.Classroom.Application.DTOs.Attendance;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.RemoveAttendance;

public sealed record RemoveAttendanceCommand(Guid Session, Guid Actor, string Role, Guid Student) : IRequest;