using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.CompleteSession;

public sealed record CompleteSessionCommand(Guid Id, Guid ActorId, bool IsAdmin) : IRequest<SessionView>;
