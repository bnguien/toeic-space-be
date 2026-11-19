using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Queries.GetScheduleAudit;

public sealed class GetScheduleAuditHandler(IClassSchedulingService scheduling)
    : IRequestHandler<GetScheduleAuditQuery, IReadOnlyList<ScheduleAuditView>>
{
    public Task<IReadOnlyList<ScheduleAuditView>> Handle(GetScheduleAuditQuery request, CancellationToken ct) =>
        scheduling.ScheduleAuditAsync(request.Id, ct);
}
