using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.CreateSchedule;

public sealed class CreateScheduleHandler(IClassSchedulingService scheduling)
    : IRequestHandler<CreateScheduleCommand, ClassSchedule>
{
    public Task<ClassSchedule> Handle(CreateScheduleCommand r, CancellationToken ct) =>
        scheduling.CreateScheduleAsync(r.Input, r.ActorId, ct);
}
