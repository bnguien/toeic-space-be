using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Queries.ListSchedules;

public sealed record ListSchedulesQuery(Guid ClassId) : IRequest<IReadOnlyList<ClassSchedule>>;
