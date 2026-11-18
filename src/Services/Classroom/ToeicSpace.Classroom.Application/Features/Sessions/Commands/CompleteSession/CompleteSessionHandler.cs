using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.CompleteSession;

public sealed class CompleteSessionHandler(IClassSchedulingService scheduling) : IRequestHandler<CompleteSessionCommand, SessionView>
{
    public Task<SessionView> Handle(CompleteSessionCommand r, CancellationToken ct) =>
        scheduling.CompleteAsync(r.Id, r.ActorId, r.IsAdmin, ct);
}
