using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Classroom.Application.Features.GoogleMeet;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/teachers/me/google-meet")]
[Authorize(Roles = "Teacher")]
public sealed class TeacherGoogleMeetController(ISender sender) : ControllerBase
{
    private Guid TeacherId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ClassRuleException("invalid_subject", "Invalid identity.", 401);

    [HttpPost("connect")]
    public async Task<object> Connect(CancellationToken ct) =>
        new { authorizationUrl = await sender.Send(new StartGoogleConnection(TeacherId), ct) };

    [HttpGet("status")]
    public Task<GoogleConnectionStatus> Status(CancellationToken ct) =>
        sender.Send(new GetGoogleConnection(TeacherId), ct);

    [HttpDelete]
    public async Task<IActionResult> Disconnect(CancellationToken ct)
    {
        await sender.Send(new DisconnectGoogleConnection(TeacherId), ct);
        return NoContent();
    }

    [HttpGet("callback"), AllowAnonymous]
    public async Task<IActionResult> Callback([FromQuery] string state, [FromQuery] string? code,
        [FromQuery] string? error, CancellationToken ct)
    {
        await sender.Send(new CompleteGoogleConnection(state, code, error), ct);
        return Ok(new { connected = true });
    }
}
