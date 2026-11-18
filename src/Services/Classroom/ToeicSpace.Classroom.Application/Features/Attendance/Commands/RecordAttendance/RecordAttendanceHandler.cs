using ToeicSpace.Classroom.Application.Interfaces.Repositories;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.RecordAttendance;

public sealed class RecordAttendanceHandler(IAttendanceRepository repository) : IRequestHandler<RecordAttendanceCommand>
{
    public Task Handle(RecordAttendanceCommand request, CancellationToken ct) =>
        repository.WriteAsync(request.Session, request.Actor, request.Role, [request.Entry], null, ct);
}