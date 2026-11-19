using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Queries.GetSession;

public sealed class GetSessionHandler(IClassSchedulingService scheduling) : IRequestHandler<GetSessionQuery, SessionView>
{
    public Task<SessionView> Handle(GetSessionQuery r, CancellationToken ct) =>
        scheduling.GetAsync(r.Id, r.ActorId, r.Role, ct);
}
