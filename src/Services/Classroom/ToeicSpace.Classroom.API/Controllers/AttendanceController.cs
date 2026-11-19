using ToeicSpace.Classroom.Domain.Enums;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.DTOs.Attendance;
using ToeicSpace.Classroom.Application.Features.Attendance.Commands.RecordAttendance;
using ToeicSpace.Classroom.Application.Features.Attendance.Commands.BulkRecordAttendance;
using ToeicSpace.Classroom.Application.Features.Attendance.Commands.RemoveAttendance;
using ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendance;
using ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendanceRoster;
using ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetAttendanceAudit;
using ToeicSpace.Classroom.Application.Features.Attendance.Queries.GetMyAttendance;
using ToeicSpace.Classroom.Domain.Entities;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Authorize(Roles = "Admin,Teacher")]
[Route("api/v1/class-sessions/{sessionId:guid}/attendance")]
public sealed class AttendanceController(ISender sender) : ControllerBase
{
    private Guid Actor => Guid.Parse(User.FindFirst(AccessTokenDefaults.SubjectClaim)!.Value);
    private string Role => User.IsInRole("Admin") ? "Admin" : "Teacher";

    [HttpGet]
    public Task<object> Records(Guid sessionId, CancellationToken ct) =>
        sender.Send(new GetAttendanceQuery(sessionId, Actor, Role), ct);

    [HttpGet("roster")]
    public Task<object> Roster(Guid sessionId, [FromQuery] string? search, CancellationToken ct) =>
        sender.Send(new GetAttendanceRosterQuery(sessionId, Actor, Role, search), ct);

    [HttpGet("audit"), Authorize(Policy = "Admin")]
    public Task<object> Audit(Guid sessionId, CancellationToken ct) =>
        sender.Send(new GetAttendanceAuditQuery(sessionId, Actor, Role), ct);

    public sealed record Entry(AttendanceStatus Status, string? Note);

    [HttpPut("{studentId:guid}")]
    public async Task<IActionResult> Record(Guid sessionId, Guid studentId, Entry body, CancellationToken ct)
    {
        await sender.Send(new RecordAttendanceCommand(sessionId, Actor, Role, new(studentId, body.Status, body.Note)), ct);
        return NoContent();
    }

    [HttpPut]
    public async Task<IActionResult> Bulk(Guid sessionId, AttendanceInputDto[] body, CancellationToken ct)
    {
        await sender.Send(new BulkRecordAttendanceCommand(sessionId, Actor, Role, body), ct);
        return NoContent();
    }

    [HttpDelete("{studentId:guid}")]
    public async Task<IActionResult> Remove(Guid sessionId, Guid studentId, CancellationToken ct)
    {
        await sender.Send(new RemoveAttendanceCommand(sessionId, Actor, Role, studentId), ct);
        return NoContent();
    }
}

[ApiController, Authorize(Roles = "User"), Route("api/v1/students/me/attendance")]
public sealed class StudentAttendanceController(ISender sender) : ControllerBase
{
    [HttpGet]
    public Task<object> History(CancellationToken ct) =>
        sender.Send(new GetMyAttendanceQuery(Guid.Parse(User.FindFirst(AccessTokenDefaults.SubjectClaim)!.Value)), ct);
}