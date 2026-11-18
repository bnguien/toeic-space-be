namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetMyAttendance;

public sealed record GetMyAttendanceQuery(Guid Student) : IRequest<object>;