using ToeicSpace.Classroom.Application.DTOs.Scheduling;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Queries.GetScheduleAudit;

public sealed record GetScheduleAuditQuery(Guid Id) : IRequest<IReadOnlyList<ScheduleAuditView>>;
