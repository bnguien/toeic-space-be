using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Queries.ListSchedules;

public sealed class ListSchedulesHandler(IClassSchedulingService scheduling)
    : IRequestHandler<ListSchedulesQuery, IReadOnlyList<ClassSchedule>>
{
    public Task<IReadOnlyList<ClassSchedule>> Handle(ListSchedulesQuery r, CancellationToken ct) =>
        scheduling.SchedulesAsync(r.ClassId, ct);
}
