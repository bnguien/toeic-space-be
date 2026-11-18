using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Queries.GetSchedule;

public sealed record GetScheduleQuery(Guid Id) : IRequest<ClassSchedule>;
