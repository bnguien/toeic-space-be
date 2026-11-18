using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.UpdateSchedule;

public sealed record UpdateScheduleCommand(Guid Id, ScheduleInput Input, int ExpectedVersion, Guid ActorId)
    : IRequest<(ClassSchedule Schedule, SessionOperationResult Sessions)>;
