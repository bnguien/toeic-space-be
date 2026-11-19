using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Features.VideoCatalog.Commands.ReportUnplayable;
using ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogHome;
using ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogPath;
using ToeicSpace.Content.Application.Features.VideoCatalog.Queries.GetCatalogTopicPage;
using ToeicSpace.Content.Application.Features.VideoWords.Queries.GetWordInfo;
using ToeicSpace.Content.Application.Features.VideoWords.Queries.GetWordMeanings;
using ToeicSpace.Content.Application.Features.VideoWords.Queries.GlossWords;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Public part of "Video song ngữ": catalog, listening path, unplayable reports, word meanings.</summary>
[ApiController]
[Route("api/v1/videos")]
[AllowAnonymous]
public sealed class VideosController(ISender sender) : ControllerBase
{
    [HttpGet("catalog")]
    [EnableRateLimiting(VideoRateLimits.Catalog)]
    public Task<CatalogHomeDto> GetCatalog(CancellationToken ct) => sender.Send(new GetCatalogHomeQuery(), ct);

    [HttpGet("catalog/{slug}")]
    [EnableRateLimiting(VideoRateLimits.Catalog)]
    public Task<CatalogTopicPageDto> GetCatalogTopic(string slug, [FromQuery] int offset = 0, [FromQuery] int limit = 12,
        CancellationToken ct = default) => sender.Send(new GetCatalogTopicPageQuery(slug, offset, limit), ct);

    [HttpPost("catalog/unplayable")]
    [EnableRateLimiting(VideoRateLimits.Unplayable)]
    public Task<UnplayableResultDto> ReportUnplayable([FromBody] ReportUnplayableCommand command, CancellationToken ct) =>
        sender.Send(command, ct);

    [HttpGet("path")]
    [EnableRateLimiting(VideoRateLimits.Catalog)]
    public Task<CatalogPathDto> GetPath([FromQuery] string? track, CancellationToken ct) =>
        sender.Send(new GetCatalogPathQuery(track), ct);

    /// <summary>level cache | free | ai — the AI tier only for signed-in learners (anonymous falls back to free).</summary>
    [HttpPost("gloss")]
    [EnableRateLimiting(VideoRateLimits.Words)]
    public Task<GlossResultDto> Gloss([FromBody] GlossWordsQuery query, CancellationToken ct) =>
        sender.Send(query with { SignedIn = User.Identity?.IsAuthenticated == true }, ct);

    /// <summary><c>?w=word</c> → word card details; <c>?words=a,b,c</c> → basic meanings of ≤ 20 words.</summary>
    [HttpGet("word-info")]
    [EnableRateLimiting(VideoRateLimits.Words)]
    public async Task<IActionResult> GetWordInfo([FromQuery] string? w, [FromQuery] string? words, CancellationToken ct) =>
        w is not null
            ? Ok(await sender.Send(new GetWordInfoQuery(w), ct))
            : Ok(await sender.Send(new GetWordMeaningsQuery(words), ct));
}
