using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.GenerateSessions;

public sealed record GenerateSessionsCommand(Guid ClassId, GenerateSessionsInput Input, Guid ActorId)
    : IRequest<SessionOperationResult>;
