using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Queries.GetSchedule;

public sealed class GetScheduleHandler(IClassSchedulingService scheduling) : IRequestHandler<GetScheduleQuery, ClassSchedule>
{
    public Task<ClassSchedule> Handle(GetScheduleQuery r, CancellationToken ct) => scheduling.GetScheduleAsync(r.Id, ct);
}
