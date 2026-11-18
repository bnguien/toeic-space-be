using ToeicSpace.Classroom.Application.Interfaces.Repositories;

namespace ToeicSpace.Classroom.Application.Features.Attendance.Commands.RemoveAttendance;

public sealed class RemoveAttendanceHandler(IAttendanceRepository repository) : IRequestHandler<RemoveAttendanceCommand>
{
    public Task Handle(RemoveAttendanceCommand request, CancellationToken ct) =>
        repository.WriteAsync(request.Session, request.Actor, request.Role, [], request.Student, ct);
}