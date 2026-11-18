using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.BulkCancelSessions;

public sealed record BulkCancelSessionsCommand(Guid ClassId, DateTime From, DateTime To, string Reason, Guid ActorId)
    : IRequest<SessionOperationResult>;
