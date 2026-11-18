using ToeicSpace.Classroom.Application.Interfaces.Repositories;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetMyAttendance;

public sealed class GetMyAttendanceHandler(IAttendanceRepository repository)
    : IRequestHandler<GetMyAttendanceQuery, object>
{
    public Task<object> Handle(GetMyAttendanceQuery request, CancellationToken ct) =>
        repository.HistoryAsync(request.Student, ct);
}