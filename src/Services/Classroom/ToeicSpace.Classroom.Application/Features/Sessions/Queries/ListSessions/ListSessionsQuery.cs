using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Queries.ListSessions;

public sealed record ListSessionsQuery(Guid ActorId, string Role, Guid? ClassId, Guid? TeacherId,
    SessionStatus? Status, DateTime From, DateTime To, int Page, int PageSize) : IRequest<PagedResult<SessionView>>;
