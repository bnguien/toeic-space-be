using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Features.Sessions.Commands.CreateSession;
using ToeicSpace.Classroom.Application.Features.Sessions.Commands.UpdateSession;
using ToeicSpace.Classroom.Application.Features.Sessions.Commands.CancelSession;
using ToeicSpace.Classroom.Application.Features.Sessions.Commands.CompleteSession;
using ToeicSpace.Classroom.Application.Features.Sessions.Queries.GetSession;
using ToeicSpace.Classroom.Application.Features.Sessions.Queries.ListSessions;
using ToeicSpace.Classroom.Application.Features.Sessions.Queries.GetSessionAudit;
using ToeicSpace.Classroom.Application.Features.StudentConflicts.Queries;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/class-sessions")]
[Authorize(Roles = "Admin,Teacher,User")]
public sealed class ClassSessionsController(ISender sender) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ClassRuleException("invalid_subject", "Invalid identity.", 401);
    private bool IsAdmin => User.IsInRole("Admin");
    private string Role => IsAdmin ? "Admin" : User.IsInRole("Teacher") ? "Teacher" : "User";

    [HttpGet]
    public Task<PagedResult<SessionView>> List([FromQuery] DateTime from, [FromQuery] DateTime to,
        [FromQuery] Guid? classId, [FromQuery] Guid? teacherId, [FromQuery] SessionStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        sender.Send(new ListSessionsQuery(ActorId, Role, classId, teacherId, status, from, to, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<SessionView> Get(Guid id, CancellationToken ct) =>
        sender.Send(new GetSessionQuery(id, ActorId, Role), ct);

    [HttpPost, Authorize(Roles = "Admin,Teacher")]
    public async Task<ActionResult<SessionView>> Create(SessionInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateSessionCommand(input, ActorId, IsAdmin), ct);
        return CreatedAtAction(nameof(Get), new { id = result.Id }, result);
    }

    [HttpPost("conflicts/preview"), Authorize(Roles = "Admin,Teacher")]
    public Task<StudentConflictPreview> PreviewCreate(SessionInput input, [FromQuery] int? detailsPage,
        [FromQuery] int detailsPageSize = 20, CancellationToken ct = default) =>
        sender.Send(new PreviewSessionCreationQuery(input, ActorId, IsAdmin,
            Details(detailsPage, detailsPageSize)), ct);

    [HttpPut("{id:guid}"), Authorize(Roles = "Admin,Teacher")]
    public Task<SessionView> Update(Guid id, SessionEdit input, CancellationToken ct) =>
        sender.Send(new UpdateSessionCommand(id, input, ActorId, IsAdmin), ct);

    [HttpPost("{id:guid}/conflicts/preview"), Authorize(Roles = "Admin,Teacher")]
    public Task<StudentConflictPreview> PreviewUpdate(Guid id, SessionEdit input, [FromQuery] int? detailsPage,
        [FromQuery] int detailsPageSize = 20, CancellationToken ct = default) =>
        sender.Send(new PreviewSessionUpdateQuery(id, input, ActorId, IsAdmin,
            Details(detailsPage, detailsPageSize)), ct);

    private ConflictDetailsRequest? Details(int? page, int pageSize)
    {
        if (!page.HasValue) return null;
        if (!IsAdmin) throw new ClassRuleException("forbidden_conflict_details", "Only Admin may view conflict details.", 403);
        return new ConflictDetailsRequest(page.Value, pageSize);
    }

    [HttpPost("{id:guid}/cancel"), Authorize(Roles = "Admin,Teacher")]
    public Task<SessionView> Cancel(Guid id, CancelRequest body, CancellationToken ct) =>
        sender.Send(new CancelSessionCommand(id, body.Reason, ActorId, IsAdmin), ct);

    [HttpPost("{id:guid}/complete"), Authorize(Roles = "Admin,Teacher")]
    public Task<SessionView> Complete(Guid id, CancellationToken ct) =>
        sender.Send(new CompleteSessionCommand(id, ActorId, IsAdmin), ct);

    [HttpGet("{id:guid}/audit"), Authorize(Policy = "Admin")]
    public Task<IReadOnlyList<SessionAuditView>> Audit(Guid id, CancellationToken ct) =>
        sender.Send(new GetSessionAuditQuery(id), ct);

    public sealed record CancelRequest(string Reason);
}
