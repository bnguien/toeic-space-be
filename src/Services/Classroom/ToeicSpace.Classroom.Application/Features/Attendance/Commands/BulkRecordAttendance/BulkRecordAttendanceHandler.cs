using ToeicSpace.Classroom.Application.Interfaces.Repositories;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.BulkRecordAttendance;

public sealed class BulkRecordAttendanceHandler(IAttendanceRepository repository)
    : IRequestHandler<BulkRecordAttendanceCommand>
{
    public Task Handle(BulkRecordAttendanceCommand request, CancellationToken ct) =>
        repository.WriteAsync(request.Session, request.Actor, request.Role, request.Items, null, ct);
}