using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.BulkCancelSessions;

public sealed class BulkCancelSessionsHandler(IClassSchedulingService scheduling)
    : IRequestHandler<BulkCancelSessionsCommand, SessionOperationResult>
{
    public Task<SessionOperationResult> Handle(BulkCancelSessionsCommand r, CancellationToken ct) =>
        scheduling.CancelRangeAsync(r.ClassId, r.From, r.To, r.Reason, r.ActorId, ct);
}
