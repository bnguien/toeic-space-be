using ToeicSpace.Classroom.Application.Interfaces.Repositories;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendanceAudit;

public sealed class GetAttendanceAuditHandler(IAttendanceRepository repository)
    : IRequestHandler<GetAttendanceAuditQuery, object>
{
    public Task<object> Handle(GetAttendanceAuditQuery request, CancellationToken ct) =>
        repository.ReadAsync(request.Session, request.Actor, request.Role, "audit", null, ct);
}