using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.Application.Features.Schedules.Commands.GenerateSessions;

public sealed class GenerateSessionsHandler(IClassSchedulingService scheduling)
    : IRequestHandler<GenerateSessionsCommand, SessionOperationResult>
{
    public Task<SessionOperationResult> Handle(GenerateSessionsCommand r, CancellationToken ct) =>
        scheduling.GenerateAsync(r.ClassId, r.Input, r.ActorId, ct);
}
