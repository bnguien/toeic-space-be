using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.CreateSchedule;

public sealed record CreateScheduleCommand(ScheduleInput Input, Guid ActorId) : IRequest<ClassSchedule>;
