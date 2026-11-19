using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.CreateSession;

public sealed record CreateSessionCommand(SessionInput Input, Guid ActorId, bool IsAdmin) : IRequest<SessionView>;
