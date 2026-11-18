using ToeicSpace.Classroom.Application.Interfaces.Repositories;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendanceRoster;

public sealed class GetAttendanceRosterHandler(IAttendanceRepository repository)
    : IRequestHandler<GetAttendanceRosterQuery, object>
{
    public Task<object> Handle(GetAttendanceRosterQuery request, CancellationToken ct) =>
        repository.ReadAsync(request.Session, request.Actor, request.Role, "roster", request.Search, ct);
}