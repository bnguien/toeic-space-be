namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendanceRoster;

public sealed record GetAttendanceRosterQuery(Guid Session, Guid Actor, string Role, string? Search) : IRequest<object>;