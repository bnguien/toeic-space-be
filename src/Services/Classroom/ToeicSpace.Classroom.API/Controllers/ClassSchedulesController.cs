using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.DTOs.Scheduling;
using ToeicSpace.Classroom.Application.Features.Schedules.Commands.CreateSchedule;
using ToeicSpace.Classroom.Application.Features.Schedules.Commands.UpdateSchedule;
using ToeicSpace.Classroom.Application.Features.Schedules.Commands.GenerateSessions;
using ToeicSpace.Classroom.Application.Features.Schedules.Commands.BulkCancelSessions;
using ToeicSpace.Classroom.Application.Features.Schedules.Queries.GetSchedule;
using ToeicSpace.Classroom.Application.Features.Schedules.Queries.ListSchedules;
using ToeicSpace.Classroom.Application.Features.Schedules.Queries.GetScheduleAudit;
using ToeicSpace.Classroom.Application.Features.StudentConflicts.Queries;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/classes/{classId:guid}/schedules")]
[Authorize(Policy = "Admin")]
public sealed class ClassSchedulesController(ISender sender) : ControllerBase
{
    private Guid AdminId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ClassRuleException("invalid_subject", "Invalid identity.", 401);

    [HttpPost]
    public async Task<ActionResult<ClassSchedule>> Create(Guid classId, ScheduleInput input, CancellationToken ct)
    {
        var result = await sender.Send(new CreateScheduleCommand(input with { ClassId = classId }, AdminId), ct);
        return CreatedAtAction(nameof(Get), new { classId, id = result.Id }, result);
    }

    [HttpPost("conflicts/preview")]
    public Task<StudentConflictPreview> PreviewCreate(Guid classId, ScheduleInput input,
        [FromQuery] int? detailsPage, [FromQuery] int detailsPageSize = 20, CancellationToken ct = default) =>
        sender.Send(new PreviewScheduleCreationQuery(input with { ClassId = classId },
            detailsPage is { } page ? new ConflictDetailsRequest(page, detailsPageSize) : null), ct);

    [HttpGet]
    public Task<IReadOnlyList<ClassSchedule>> List(Guid classId, CancellationToken ct) =>
        sender.Send(new ListSchedulesQuery(classId), ct);

    [HttpGet("{id:guid}")]
    public async Task<ClassSchedule> Get(Guid classId, Guid id, CancellationToken ct)
    {
        var result = await sender.Send(new GetScheduleQuery(id), ct);
        if (result.ClassId != classId) throw new ClassRuleException("schedule_not_found", "Schedule not found.", 404);
        return result;
    }

    [HttpPut("{id:guid}")]
    public async Task<ScheduleUpdateResponse> Update(Guid classId, Guid id, UpdateScheduleRequest body, CancellationToken ct)
    {
        var (schedule, sessions) = await sender.Send(new UpdateScheduleCommand(id,
            body.Input with { ClassId = classId }, body.ExpectedVersion, AdminId), ct);
        return new ScheduleUpdateResponse(schedule, sessions);
    }

    [HttpPost("{id:guid}/conflicts/preview")]
    public Task<StudentConflictPreview> PreviewUpdate(Guid classId, Guid id, UpdateScheduleRequest body,
        [FromQuery] int? detailsPage, [FromQuery] int detailsPageSize = 20, CancellationToken ct = default) =>
        sender.Send(new PreviewScheduleUpdateQuery(id, body.Input with { ClassId = classId }, body.ExpectedVersion,
            detailsPage is { } page ? new ConflictDetailsRequest(page, detailsPageSize) : null), ct);

    [HttpGet("{id:guid}/audit")]
    public async Task<IReadOnlyList<ScheduleAuditView>> Audit(Guid classId, Guid id, CancellationToken ct)
    {
        var schedule = await sender.Send(new GetScheduleQuery(id), ct);
        if (schedule.ClassId != classId) throw new ClassRuleException("schedule_not_found", "Schedule not found.", 404);
        return await sender.Send(new GetScheduleAuditQuery(id), ct);
    }

    [HttpPost("generate")]
    public Task<SessionOperationResult> Generate(Guid classId, GenerateSessionsInput body, CancellationToken ct) =>
        sender.Send(new GenerateSessionsCommand(classId, body, AdminId), ct);

    [HttpPost("generate/conflicts/preview")]
    public Task<StudentConflictPreview> PreviewGenerate(Guid classId, GenerateSessionsInput body,
        [FromQuery] int? detailsPage, [FromQuery] int detailsPageSize = 20, CancellationToken ct = default) =>
        sender.Send(new PreviewSessionGenerationQuery(classId, body,
            detailsPage is { } page ? new ConflictDetailsRequest(page, detailsPageSize) : null), ct);

    [HttpPost("cancel-sessions")]
    public Task<SessionOperationResult> BulkCancel(Guid classId, CancelRangeRequest body, CancellationToken ct) =>
        sender.Send(new BulkCancelSessionsCommand(classId, body.From, body.To, body.Reason, AdminId), ct);

    public sealed record UpdateScheduleRequest(ScheduleInput Input, int ExpectedVersion);
    public sealed record ScheduleUpdateResponse(ClassSchedule Schedule, SessionOperationResult Sessions);
    public sealed record CancelRangeRequest(DateTime From, DateTime To, string Reason);
}
