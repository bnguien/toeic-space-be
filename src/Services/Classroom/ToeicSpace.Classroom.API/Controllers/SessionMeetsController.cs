using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.Features.GoogleMeet;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/class-sessions/{sessionId:guid}/meet")]
[Authorize(Roles = "Admin,Teacher,User")]
public sealed class SessionMeetsController(ISender sender) : ControllerBase
{
    private Guid ActorId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ClassRuleException("invalid_subject", "Invalid identity.", 401);
    private string Role => User.IsInRole("Admin") ? "Admin" : User.IsInRole("Teacher") ? "Teacher" : "User";

    [HttpGet("status")]
    public Task<MeetStatus> Status(Guid sessionId, CancellationToken ct) =>
        sender.Send(new GetMeetStatus(sessionId, ActorId, Role), ct);

    [HttpGet("link")]
    public Task<MeetLink> Link(Guid sessionId, CancellationToken ct) =>
        sender.Send(new GetMeetLink(sessionId, ActorId, Role), ct);

    [HttpPost("retry"), Authorize(Policy = "Admin")]
    public async Task<IActionResult> Retry(Guid sessionId, CancellationToken ct)
    {
        await sender.Send(new RetryMeetProvisioning(sessionId), ct);
        return Accepted();
    }
}
