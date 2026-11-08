using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.Features.DictationLearner.Commands.UpdateDictationLearnerState;
using ToeicSpace.Content.Application.Features.DictationLearner.Queries.GetDictationLearnerState;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>The signed-in learner's dictation state (stars, study seconds, settings, voice, sound).</summary>
[ApiController]
[Route("api/v1/dictation/me")]
[Authorize]
public sealed class LearnerDictationController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet("state")]
    public Task<DictationLearnerStateDto> State(CancellationToken ct) =>
        sender.Send(new GetDictationLearnerStateQuery(LearnerId), ct);

    [HttpPut("state")]
    public Task<DictationLearnerStateDto> UpdateState([FromBody] UpdateDictationLearnerStateCommand command,
        CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);
}
