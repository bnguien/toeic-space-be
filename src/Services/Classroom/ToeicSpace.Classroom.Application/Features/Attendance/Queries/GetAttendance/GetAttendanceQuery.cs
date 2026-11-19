namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendance;

public sealed record GetAttendanceQuery(Guid Session, Guid Actor, string Role) : IRequest<object>;