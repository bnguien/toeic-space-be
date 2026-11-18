using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Commands.UpdateSession;

public sealed record UpdateSessionCommand(Guid Id, SessionEdit Input, Guid ActorId, bool IsAdmin) : IRequest<SessionView>;
