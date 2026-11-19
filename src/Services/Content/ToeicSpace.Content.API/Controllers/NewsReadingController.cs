using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.Features.News.Commands.CompleteNewsArticle;
using ToeicSpace.Content.Application.Features.News.Commands.MergeGuestReadings;
using ToeicSpace.Content.Application.Features.News.Commands.OpenNewsArticle;
using ToeicSpace.Content.Application.Features.News.Commands.UpdateNewsReadingState;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>The signed-in learner's reading of news articles (any role; learner = JWT <c>sub</c>).</summary>
[ApiController]
[Route("api/v1/news")]
[Authorize]
[EnableRateLimiting(NewsRateLimits.Write)]
public sealed class NewsReadingController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost("articles/{slug}/open")]
    public Task<NewsReadingStateDto> Open(string slug, CancellationToken ct) =>
        sender.Send(new OpenNewsArticleCommand(slug, LearnerId), ct);

    [HttpPost("articles/{slug}/complete")]
    public Task<NewsReadingStateDto> Complete(string slug, CancellationToken ct) =>
        sender.Send(new CompleteNewsArticleCommand(slug, LearnerId), ct);

    [HttpPut("articles/{slug}/state")]
    public Task<NewsReadingStateDto> UpdateState(string slug, [FromBody] UpdateReadingStateRequest body,
        CancellationToken ct) =>
        sender.Send(new UpdateNewsReadingStateCommand(slug, body.ReadingSeconds, body.KnownWords, body.Notes,
            body.Dictation, body.ResetDictation, body.Settings, LearnerId), ct);

    /// <summary>Merges the reading state a guest kept in the browser into the account (after sign-in).</summary>
    [HttpPost("me/merge")]
    public Task<NewsMergeResultDto> Merge([FromBody] MergeGuestReadingsCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);
}
