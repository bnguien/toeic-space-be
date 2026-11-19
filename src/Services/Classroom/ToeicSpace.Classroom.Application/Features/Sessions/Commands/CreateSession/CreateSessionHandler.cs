using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.CreateSession;

public sealed class CreateSessionHandler(IClassSchedulingService scheduling) : IRequestHandler<CreateSessionCommand, SessionView>
{
    public Task<SessionView> Handle(CreateSessionCommand r, CancellationToken ct) =>
        scheduling.CreateAsync(r.Input, r.ActorId, r.IsAdmin, ct);
}
