using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.UpdateSession;

public sealed class UpdateSessionHandler(IClassSchedulingService scheduling) : IRequestHandler<UpdateSessionCommand, SessionView>
{
    public Task<SessionView> Handle(UpdateSessionCommand r, CancellationToken ct) =>
        scheduling.UpdateAsync(r.Id, r.Input, r.ActorId, r.IsAdmin, ct);
}
