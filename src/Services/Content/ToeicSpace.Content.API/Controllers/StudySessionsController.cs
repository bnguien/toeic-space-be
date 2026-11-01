using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.StudySessions;
using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AbandonStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.AnswerStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.CompleteStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Commands.StartStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetActiveStudySession;
using ToeicSpace.Content.Application.Features.StudySessions.Queries.GetStudySession;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;
using ToeicSpace.Content.Domain.Enums;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Server-side study sessions and personal preferences of the signed-in learner.</summary>
[ApiController]
[Route("api/v1/vocabulary/me")]
[Authorize]
public sealed class StudySessionsController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    /// <summary>201 for a new session, 200 when the active one is resumed.</summary>
    [HttpPost("sessions")]
    public async Task<ActionResult<StudySessionDto>> Start([FromBody] StartStudySessionCommand command,
        CancellationToken ct)
    {
        var result = await sender.Send(command with { LearnerId = LearnerId }, ct);
        return result.Created ? StatusCode(StatusCodes.Status201Created, result.Session) : Ok(result.Session);
    }

    /// <summary>The most recent active session matching the filters, or 204.</summary>
    [HttpGet("sessions/active")]
    public async Task<ActionResult<StudySessionDto>> Active([FromQuery] StudyMode? mode, [FromQuery] Guid? setId,
        [FromQuery] Guid? folderId, [FromQuery] bool? library, [FromQuery] bool? due, [FromQuery] int tzOffset = 0,
        CancellationToken ct = default)
    {
        var session = await sender.Send(new GetActiveStudySessionQuery(LearnerId, mode, setId, folderId, library, due,
            tzOffset), ct);
        return session is null ? NoContent() : Ok(session);
    }

    [HttpGet("sessions/{sessionId:guid}")]
    public Task<StudySessionDto> Get(Guid sessionId, [FromQuery] int tzOffset = 0, CancellationToken ct = default) =>
        sender.Send(new GetStudySessionQuery(LearnerId, sessionId, tzOffset), ct);

    [HttpPost("sessions/{sessionId:guid}/answers")]
    public Task<StudySessionDto> Answer(Guid sessionId, [FromBody] AnswerStudySessionCommand command,
        CancellationToken ct) =>
        sender.Send(command with { SessionId = sessionId, LearnerId = LearnerId }, ct);

    [HttpPost("sessions/{sessionId:guid}/complete")]
    public Task<StudySessionDto> Complete(Guid sessionId, [FromQuery] int tzOffset = 0,
        CancellationToken ct = default) =>
        sender.Send(new CompleteStudySessionCommand(sessionId, LearnerId, tzOffset), ct);

    [HttpDelete("sessions/{sessionId:guid}")]
    public async Task<IActionResult> Abandon(Guid sessionId, CancellationToken ct)
    {
        await sender.Send(new AbandonStudySessionCommand(sessionId, LearnerId), ct);
        return NoContent();
    }

    [HttpGet("preferences")]
    public Task<PreferencesDto> Preferences(CancellationToken ct) =>
        sender.Send(new GetPreferencesQuery(LearnerId), ct);

    [HttpPut("preferences")]
    public Task<PreferencesDto> UpdatePreferences([FromBody] UpdatePreferencesCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);
}
