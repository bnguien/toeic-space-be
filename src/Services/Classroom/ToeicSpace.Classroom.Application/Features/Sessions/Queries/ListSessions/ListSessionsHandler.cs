using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Queries.ListSessions;

public sealed class ListSessionsHandler(IClassSchedulingService scheduling)
    : IRequestHandler<ListSessionsQuery, PagedResult<SessionView>>
{
    public Task<PagedResult<SessionView>> Handle(ListSessionsQuery r, CancellationToken ct) =>
        scheduling.ListAsync(r.ActorId, r.Role, r.ClassId, r.TeacherId, r.Status,
            r.From, r.To, r.Page, r.PageSize, ct);
}
