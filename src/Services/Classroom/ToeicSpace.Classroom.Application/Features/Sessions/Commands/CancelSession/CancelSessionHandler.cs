using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.CancelSession;

public sealed class CancelSessionHandler(IClassSchedulingService scheduling) : IRequestHandler<CancelSessionCommand, SessionView>
{
    public Task<SessionView> Handle(CancelSessionCommand r, CancellationToken ct) =>
        scheduling.CancelAsync(r.Id, r.Reason, r.ActorId, r.IsAdmin, ct);
}
