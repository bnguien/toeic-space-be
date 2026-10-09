using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.Application.Features.Profile.Commands;
using ToeicSpace.Identity.Application.Features.Profile.Queries;
using ToeicSpace.Identity.Application.Features.Profile.Results;

namespace ToeicSpace.Identity.API.Controllers;

[ApiController]
[Route("api/v1/users")]
[Produces("application/json")]
public sealed class UserController : ControllerBase
{
    private readonly IMediator _mediator;

    public UserController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("me")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Get current user profile",
        Description = "Retrieves the full profile information of the currently authenticated user.")]
    [ProducesResponseType(typeof(UserProfileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> GetMyProfile(CancellationToken cancellationToken)
    {
        var subjectClaim = User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value;
        if (!Guid.TryParse(subjectClaim, out var userId))
        {
            return Unauthorized();
        }

        var result = await _mediator.Send(new GetProfileQuery(userId), cancellationToken);
        return Ok(result);
    }

    [HttpPut("me")]
    [Authorize]
    [SwaggerOperation(
        Summary = "Update current user profile",
        Description = "Updates profile fields. UserId is securely extracted from the token.")]
    [ProducesResponseType(typeof(UserProfileResult), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> UpdateMyProfile(
        [FromBody] UpdateProfileCommand command,
        CancellationToken cancellationToken)
    {
        var subjectClaim = User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value;
        if (!Guid.TryParse(subjectClaim, out var userId))
        {
            return Unauthorized();
        }

        command.UserId = userId;
        var result = await _mediator.Send(command, cancellationToken);
        return Ok(result);
    }
    
    [HttpPost("me/avatar")]
    public async Task<IActionResult> GetAvatarUploadUrl([FromBody] UploadAvatarCommand command)
    {
        var subjectClaim = User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value;
        if (!Guid.TryParse(subjectClaim, out var userId)) return Unauthorized();

        command.UserId = userId;
    
        var uploadUrl = await _mediator.Send(command);

        return Ok(new { 
            message = "Upload URL generated successfully.",
            uploadUrl = uploadUrl 
        });
    }
}
