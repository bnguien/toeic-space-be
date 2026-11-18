using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.CancelSession;

public sealed record CancelSessionCommand(Guid Id, string Reason, Guid ActorId, bool IsAdmin) : IRequest<SessionView>;
