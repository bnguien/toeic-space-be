using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Interfaces.Services;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.Application.Features.Sessions.Queries.GetSessionAudit;

public sealed record GetSessionAuditQuery(Guid Id) : IRequest<IReadOnlyList<SessionAuditView>>;
