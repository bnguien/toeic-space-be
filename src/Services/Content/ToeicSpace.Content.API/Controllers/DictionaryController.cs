using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.Dictionary;
using ToeicSpace.Content.Application.Features.Dictionary.Queries.LookupSelection;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>
/// "Bôi đen để tra": looks up the text a learner selects on any page. Open to guests (fast tiers only);
/// a token enables the AI pass and tells whether the word is already in the learner's library.
/// </summary>
[ApiController]
[Route("api/v1/dictionary")]
[AllowAnonymous]
public sealed class DictionaryController(ISender sender) : ControllerBase
{
    private Guid OptionalLearnerId =>
        Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id) ? id : Guid.Empty;

    [HttpPost("lookup")]
    [EnableRateLimiting(DictionaryRateLimits.Lookup)]
    public Task<SelectionLookupDto> Lookup([FromBody] LookupSelectionQuery query, CancellationToken ct) =>
        sender.Send(query with { LearnerId = OptionalLearnerId }, ct);
}
