using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.UpdateSchedule;

public sealed class UpdateScheduleHandler(IClassSchedulingService scheduling)
    : IRequestHandler<UpdateScheduleCommand, (ClassSchedule Schedule, SessionOperationResult Sessions)>
{
    public Task<(ClassSchedule Schedule, SessionOperationResult Sessions)> Handle(UpdateScheduleCommand r, CancellationToken ct) =>
        scheduling.UpdateScheduleAsync(r.Id, r.Input, r.ExpectedVersion, r.ActorId, ct);
}
