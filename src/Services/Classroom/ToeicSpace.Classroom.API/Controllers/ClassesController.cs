using ToeicSpace.Classroom.Application.Common.Pagination;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.AssignTeacher;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.ChangeClassStatus;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.CreateClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.DeleteClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.HardDeleteClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreDeletedClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.RestoreArchivedClass;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.UnassignTeacher;
using ToeicSpace.Classroom.Application.Features.Classes.Commands.UpdateClass;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClassById;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetClasses;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetDeletedClasses;
using ToeicSpace.Classroom.Application.Features.Classes.Queries.GetMyClasses;
using ToeicSpace.Classroom.Application.DTOs.Classes;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/classes")]
[Authorize(Policy = "AdminOrTeacher")]
public sealed class ClassesController(ISender sender) : ControllerBase
{
    private Guid UserId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id
        : throw new ClassRuleException("invalid_subject", "Invalid user identity.", 401);

    private bool IsAdmin => User.IsInRole("Admin");

    [HttpPost, Authorize(Policy = "Admin")]
    public async Task<ActionResult<ClassDetailDto>> Create(CreateClassCommand command, CancellationToken ct)
    {
        command.AdminId = UserId;
        var result = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Class.Id }, result);
    }

    [HttpGet, Authorize(Policy = "Admin")]
    public Task<PagedResult<ClassDto>> List([FromQuery] GetClassesQuery query, CancellationToken ct) =>
        sender.Send(query, ct);

    [HttpGet("deleted"), Authorize(Policy = "Admin")]
    public Task<PagedResult<ClassDto>> Deleted([FromQuery] GetDeletedClassesQuery query, CancellationToken ct) =>
        sender.Send(query, ct);

    [HttpGet("me"), Authorize(Roles = "Teacher")]
    public Task<PagedResult<ClassDto>> Mine([FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) => sender.Send(new GetMyClassesQuery(UserId, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<ClassDetailDto> Get(Guid id, CancellationToken ct) =>
        sender.Send(new GetClassByIdQuery(id, IsAdmin ? null : UserId), ct);

    [HttpPatch("{id:guid}"), Authorize(Policy = "Admin")]
    public Task<ClassDetailDto> Update(Guid id, UpdateClassCommand command, CancellationToken ct)
    {
        command.ClassId = id;
        command.AdminId = UserId;
        return sender.Send(command, ct);
    }

    [HttpPatch("{id:guid}/status"), Authorize(Policy = "Admin")]
    public Task<ClassDetailDto> Status(Guid id, ChangeClassStatusCommand command, CancellationToken ct)
    {
        command.ClassId = id;
        command.AdminId = UserId;
        return sender.Send(command, ct);
    }

    [HttpPost("{id:guid}/archive"), Authorize(Policy = "Admin")]
    public Task<ClassDetailDto> Archive(Guid id, CancellationToken ct) =>
        sender.Send(new ChangeClassStatusCommand(ClassStatus.Archived) { ClassId = id, AdminId = UserId }, ct);

    [HttpPost("{id:guid}/restore"), Authorize(Policy = "Admin")]
    public Task<ClassDetailDto> Restore(Guid id, CancellationToken ct) =>
        sender.Send(new RestoreArchivedClassCommand(id, UserId), ct);

    [HttpDelete("{id:guid}"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await sender.Send(new DeleteClassCommand(id, UserId), ct);
        return NoContent();
    }

    [HttpPost("{id:guid}/admin-restore"), Authorize(Policy = "Admin")]
    public Task<ClassDetailDto> AdminRestore(Guid id, CancellationToken ct) =>
        sender.Send(new RestoreDeletedClassCommand(id, UserId), ct);

    [HttpDelete("{id:guid}/permanent"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> HardDelete(Guid id, CancellationToken ct)
    {
        await sender.Send(new HardDeleteClassCommand(id), ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/teacher"), Authorize(Policy = "Admin")]
    public Task<ClassDetailDto> Assign(Guid id, AssignTeacherCommand command, CancellationToken ct)
    {
        command.ClassId = id;
        command.AdminId = UserId;
        return sender.Send(command, ct);
    }

    [HttpDelete("{id:guid}/teacher"), Authorize(Policy = "Admin")]
    public Task<ClassDetailDto> Unassign(Guid id, CancellationToken ct) =>
        sender.Send(new UnassignTeacherCommand(id, UserId), ct);
}
