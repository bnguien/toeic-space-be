using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Queries.GetSessionAudit;

public sealed class GetSessionAuditHandler(IClassSchedulingService scheduling)
    : IRequestHandler<GetSessionAuditQuery, IReadOnlyList<SessionAuditView>>
{
    public Task<IReadOnlyList<SessionAuditView>> Handle(GetSessionAuditQuery r, CancellationToken ct) =>
        scheduling.AuditAsync(r.Id, ct);
}
