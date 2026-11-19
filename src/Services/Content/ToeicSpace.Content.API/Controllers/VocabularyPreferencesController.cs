using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.VocabularyPreferences;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Commands.UpdatePreferences;
using ToeicSpace.Content.Application.Features.VocabularyPreferences.Queries.GetPreferences;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Personal vocabulary preferences of the signed-in learner (workspace and study settings).</summary>
[ApiController]
[Route("api/v1/vocabulary/me/preferences")]
[Authorize]
public sealed class VocabularyPreferencesController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet]
    public Task<PreferencesDto> Get(CancellationToken ct) => sender.Send(new GetPreferencesQuery(LearnerId), ct);

    /// <summary>Partial update; <c>study</c> is merged field by field.</summary>
    [HttpPut]
    public Task<PreferencesDto> Update([FromBody] UpdatePreferencesCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);
}
