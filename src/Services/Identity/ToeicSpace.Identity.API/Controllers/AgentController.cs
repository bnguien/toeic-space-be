using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.Identity.API.Assistant;
using ToeicSpace.Identity.API.Security;
using ToeicSpace.Identity.Application.Interfaces.Security;

namespace ToeicSpace.Identity.API.Controllers;

/// <summary>
/// The AI assistant trades its access key for a short, read-only token (scope agent.read). Disabled (404) until
/// AgentAccess:KeySha256 and AgentAccess:SubjectId are configured.
/// </summary>
[ApiController]
[Route("api/v1/agent")]
[Produces("application/json")]
public sealed class AgentController : ControllerBase
{
    private const int MaxKeyLength = 200;

    private readonly IAccessTokenIssuer _issuer;
    private readonly AgentAccessOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<AgentController> _logger;

    public AgentController(
        IAccessTokenIssuer issuer,
        AgentAccessOptions options,
        TimeProvider timeProvider,
        ILogger<AgentController> logger)
    {
        _issuer = issuer;
        _options = options;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    /// <summary>Issues a read-only assistant token.</summary>
    /// <param name="key">The assistant access key (X-Agent-Key); only its SHA-256 is configured here.</param>
    /// <param name="onBehalfOf">
    /// Optional X-On-Behalf-Of: ID of the person the assistant works for, 1-64 characters of
    /// <c>[A-Za-z0-9_:-]</c>. Copied into the token's <c>act</c> claim for audit only and never trusted for
    /// authorization: the token stays role Admin with scope agent.read (GET/HEAD only) whatever the value.
    /// </param>
    /// <param name="onBehalfRole">
    /// Optional X-On-Behalf-Role: must be <c>admin</c>, the only role an assistant may act for. Requires X-On-Behalf-Of.
    /// </param>
    /// <response code="200"><c>{ accessToken, expiresAt }</c>.</response>
    /// <response code="400">X-On-Behalf-Of or X-On-Behalf-Role is malformed.</response>
    /// <response code="401">Missing or wrong key.</response>
    /// <response code="404">Assistant access is not configured.</response>
    [HttpPost("token")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Credentials)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public IActionResult IssueToken(
        [FromHeader(Name = "X-Agent-Key")] string? key,
        [FromHeader(Name = AgentOnBehalf.OfHeader)] string? onBehalfOf,
        [FromHeader(Name = AgentOnBehalf.RoleHeader)] string? onBehalfRole)
    {
        if (string.IsNullOrWhiteSpace(_options.KeySha256) || _options.SubjectId == Guid.Empty)
        {
            return NotFound();
        }

        byte[] expected;
        try
        {
            expected = Convert.FromHexString(_options.KeySha256.Trim());
        }
        catch (FormatException)
        {
            return NotFound();
        }

        if (string.IsNullOrEmpty(key) || key.Length > MaxKeyLength
            || !CryptographicOperations.FixedTimeEquals(SHA256.HashData(Encoding.UTF8.GetBytes(key)), expected))
        {
            return Unauthorized();
        }

        // Checked after the key, so an unauthenticated caller learns nothing about the header rules.
        if (!AgentOnBehalf.TryParse(onBehalfOf, onBehalfRole, out var actor, out var error))
        {
            return Problem(statusCode: StatusCodes.Status400BadRequest, title: "Bad Request", detail: error);
        }

        var token = _issuer.IssueForAgent(_options.SubjectId, _timeProvider.GetUtcNow(), _options.TokenLifetime, actor);

        if (actor is not null)
        {
            _logger.LogInformation("Assistant token issued on behalf of {Actor}", actor);
        }

        return Ok(new { accessToken = token.Token, expiresAt = token.ExpiresAt });
    }
}
