using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Queries.GetSession;

public sealed record GetSessionQuery(Guid Id, Guid ActorId, string Role) : IRequest<SessionView>;
