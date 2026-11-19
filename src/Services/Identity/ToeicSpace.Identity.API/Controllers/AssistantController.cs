using System.ComponentModel.DataAnnotations;
using System.Text.RegularExpressions;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Caching.Memory;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Identity.API.Assistant;
using ToeicSpace.Identity.API.Security;
using ToeicSpace.Identity.Application.Features.Profiles.Queries.GetProfile;

namespace ToeicSpace.Identity.API.Controllers;

/// <summary>One earlier turn the chat panel shows, so a follow-up question ("còn Part 2 thì sao?") is understood.</summary>
public sealed record AssistantTurn(
    [Required, RegularExpression("^(user|assistant)$")] string Role,
    [Required, StringLength(1500)] string Text);

public sealed record AskAssistantRequest(
    [Required, StringLength(2000, MinimumLength = 1)] string Text,
    [MaxLength(6)] IReadOnlyList<AssistantTurn>? Context = null);

public sealed record DecideApprovalRequest(bool Approved);

/// <summary>
/// "Ask AI" of the admin area: forwards the administrator's question to SeasonedAgents and lets the panel follow the
/// task. The browser never sees the integration key; SeasonedAgents never sees a TOEIC token.
/// </summary>
[ApiController]
[Route("api/v1/admin/assistant")]
[Authorize(Roles = "Admin")]
[EnableRateLimiting(RateLimitPolicies.Assistant)]
[RequestSizeLimit(32_768)]
[Produces("application/json")]
public sealed partial class AssistantController : ControllerBase
{
    /// <summary>
    /// Blocks the panel may render inside an answer: SeasonedAgents then adds <c>[[chart:{…}]]</c> and
    /// <c>[[stat:{…}]]</c> lines. Fixed on the server, never taken from the browser.
    /// </summary>
    private static readonly string[] RenderBlocks = ["chart", "stat"];

    /// <summary>The integration key decides the SeasonedAgents organisation and there is one key, so one roster.</summary>
    private const string RosterCacheKey = "assistant:agents";

    private static readonly TimeSpan RosterCacheDuration = TimeSpan.FromSeconds(120);

    private readonly SeasonedAgentsClient _agents;
    private readonly ISender _sender;
    private readonly IMemoryCache _cache;
    private readonly ILogger<AssistantController> _logger;

    public AssistantController(
        SeasonedAgentsClient agents,
        ISender sender,
        IMemoryCache cache,
        ILogger<AssistantController> logger)
    {
        _agents = agents;
        _sender = sender;
        _cache = cache;
        _logger = logger;
    }

    [GeneratedRegex("^[A-Za-z0-9_:-]{1,64}$")]
    private static partial Regex SafeId();

    [HttpPost("messages")]
    public Task<IActionResult> Ask([FromBody] AskAssistantRequest request, CancellationToken ct)
        => ForwardAsync(
            HttpMethod.Post,
            "conversations/messages",
            new
            {
                text = request.Text.Trim(),
                context = (request.Context ?? []).Select(turn => new { role = turn.Role, text = turn.Text.Trim() }),
                render = RenderBlocks
            },
            ct);

    /// <summary>
    /// The organisation's active, non-core agents: code, name, kind, role, capabilities, status, persona, uses and
    /// wins. Internal IDs, instructions and scores are stripped. Cached for 120 seconds.
    /// </summary>
    [HttpGet("agents")]
    [ProducesResponseType(typeof(IReadOnlyList<AssistantAgent>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAgents(CancellationToken ct)
    {
        if (!_agents.Enabled)
        {
            return NotConfigured();
        }

        if (_cache.TryGetValue(RosterCacheKey, out IReadOnlyList<AssistantAgent>? cached) && cached is not null)
        {
            return Ok(cached);
        }

        var admin = await ResolveActingAdminAsync(ct);
        if (admin is null)
        {
            return Unauthorized();
        }

        AssistantReply reply;
        try
        {
            reply = await _agents.SendAsync(HttpMethod.Get, "agents", null, admin, ct);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(error, "SeasonedAgents is unreachable");
            return Unreachable();
        }

        if (reply.Status is < 200 or >= 300)
        {
            return Relay(reply);
        }

        var roster = AgentRoster.Parse(reply.Body);
        if (roster is null)
        {
            _logger.LogWarning("SeasonedAgents returned an agent roster that could not be read");
            return Problem(statusCode: StatusCodes.Status502BadGateway, title: "Danh sách trợ lý không đọc được.");
        }

        _cache.Set(RosterCacheKey, roster, RosterCacheDuration);
        return Ok(roster);
    }

    [HttpGet("tasks/{taskId}")]
    public Task<IActionResult> GetTask(string taskId, CancellationToken ct)
        => SafeId().IsMatch(taskId)
            ? ForwardAsync(HttpMethod.Get, $"tasks/{taskId}", null, ct)
            : Task.FromResult<IActionResult>(BadRequest());

    [HttpGet("approvals")]
    public Task<IActionResult> GetApprovals(CancellationToken ct)
        => ForwardAsync(HttpMethod.Get, "approvals", null, ct);

    [HttpPost("approvals/{approvalId}/decide")]
    public Task<IActionResult> Decide(string approvalId, [FromBody] DecideApprovalRequest request, CancellationToken ct)
        => SafeId().IsMatch(approvalId)
            ? ForwardAsync(HttpMethod.Post, $"approvals/{approvalId}/decide", new { approved = request.Approved }, ct)
            : Task.FromResult<IActionResult>(BadRequest());

    private async Task<IActionResult> ForwardAsync(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        if (!_agents.Enabled)
        {
            return NotConfigured();
        }

        var admin = await ResolveActingAdminAsync(ct);
        if (admin is null)
        {
            return Unauthorized();
        }

        try
        {
            return Relay(await _agents.SendAsync(method, path, body, admin, ct));
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !ct.IsCancellationRequested)
        {
            _logger.LogWarning(error, "SeasonedAgents is unreachable");
            return Unreachable();
        }
    }

    /// <summary>The signed-in admin, sent to SeasonedAgents in the acting headers. Null without a valid subject.</summary>
    private async Task<ActingAdmin?> ResolveActingAdminAsync(CancellationToken ct)
    {
        if (!Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var userId))
        {
            return null;
        }

        var email = await _cache.GetOrCreateAsync($"assistant:email:{userId}", async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var profile = await _sender.Send(new GetProfileQuery(userId), ct);
            return profile.Email;
        }) ?? string.Empty;

        return new ActingAdmin(userId.ToString(), email);
    }

    private static ContentResult Relay(AssistantReply reply)
        => new()
        {
            StatusCode = reply.Status >= 500 ? StatusCodes.Status502BadGateway : reply.Status,
            Content = reply.Body,
            ContentType = "application/json"
        };

    private ObjectResult NotConfigured()
        => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Trợ lý AI chưa được cấu hình.");

    private ObjectResult Unreachable()
        => Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "Trợ lý AI đang không kết nối được.");
}
