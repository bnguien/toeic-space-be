namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendanceAudit;

public sealed record GetAttendanceAuditQuery(Guid Session, Guid Actor, string Role) : IRequest<object>;