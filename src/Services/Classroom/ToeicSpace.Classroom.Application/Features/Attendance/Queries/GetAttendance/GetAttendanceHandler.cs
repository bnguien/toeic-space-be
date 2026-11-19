using ToeicSpace.Classroom.Application.Interfaces.Repositories;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendance;

public sealed class GetAttendanceHandler(IAttendanceRepository repository) : IRequestHandler<GetAttendanceQuery, object>
{
    public Task<object> Handle(GetAttendanceQuery request, CancellationToken ct) =>
        repository.ReadAsync(request.Session, request.Actor, request.Role, "records", null, ct);
}