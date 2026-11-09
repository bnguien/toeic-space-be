using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Features.GrammarComments.Commands.CreateComment;
using ToeicSpace.Content.Application.Features.GrammarComments.Commands.DeleteComment;
using ToeicSpace.Content.Application.Features.GrammarComments.Commands.LikeComment;
using ToeicSpace.Content.Application.Features.GrammarComments.Queries.GetCommentReplies;
using ToeicSpace.Content.Application.Features.GrammarComments.Queries.GetComments;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Comments on a grammar topic document: anyone reads, signed-in learners write.</summary>
[ApiController]
[Route("api/v1/grammar/comments")]
public sealed class GrammarCommentsController(ISender sender) : ControllerBase
{
    /// <summary>Display name stored with a comment when the token carries no <c>name</c> claim.</summary>
    public sealed record CreateCommentRequest(string Target, string Content, long? ParentId, string? AuthorName);

    public sealed record LikeRequest(bool Liked);

    private const string DefaultAuthorName = "Học viên";

    private Guid? ViewerId => User.Identity?.IsAuthenticated == true
        && Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id) ? id : null;

    private Guid LearnerId => ViewerId ?? throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet]
    [AllowAnonymous]
    public Task<CommentPageDto> List([FromQuery] string target, [FromQuery] long? before, CancellationToken ct) =>
        sender.Send(new GetCommentsQuery(target ?? string.Empty, before, ViewerId), ct);

    [HttpGet("{id:long}/replies")]
    [AllowAnonymous]
    public Task<IReadOnlyList<CommentDto>> Replies(long id, CancellationToken ct) =>
        sender.Send(new GetCommentRepliesQuery(id, ViewerId), ct);

    [HttpPost]
    [Authorize]
    [EnableRateLimiting(GrammarRateLimits.Comment)]
    public async Task<IActionResult> Create([FromBody] CreateCommentRequest request, CancellationToken ct)
    {
        var name = User.FindFirst("name")?.Value ?? request.AuthorName;
        name = string.IsNullOrWhiteSpace(name) ? DefaultAuthorName : name.Trim();
        var command = new CreateCommentCommand(request.Target, request.Content ?? string.Empty, request.ParentId)
        {
            LearnerId = LearnerId, AuthorName = name.Length > 80 ? name[..80] : name
        };
        return StatusCode(StatusCodes.Status201Created, await sender.Send(command, ct));
    }

    [HttpDelete("{id:long}")]
    [Authorize]
    public Task<CommentDeleteDto> Delete(long id, CancellationToken ct) =>
        sender.Send(new DeleteCommentCommand(id, LearnerId, User.IsInRole("Admin")), ct);

    [HttpPost("{id:long}/like")]
    [Authorize]
    public Task<CommentLikeDto> Like(long id, [FromBody] LikeRequest request, CancellationToken ct) =>
        sender.Send(new LikeCommentCommand(request.Liked) { CommentId = id, LearnerId = LearnerId }, ct);
}
