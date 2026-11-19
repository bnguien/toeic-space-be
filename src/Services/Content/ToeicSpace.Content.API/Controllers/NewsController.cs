using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Net.Http.Headers;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.News;
using ToeicSpace.Content.Application.DTOs.VocabularyLookup;
using ToeicSpace.Content.Application.Features.News.Commands.CheckNewsDictation;
using ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticle;
using ToeicSpace.Content.Application.Features.News.Queries.GetNewsArticles;
using ToeicSpace.Content.Application.Features.News.Queries.LookupNewsWord;
using ToeicSpace.Content.Application.Interfaces.Services;
using ToeicSpace.Content.Domain.Exceptions;
using ToeicSpace.Content.Domain.Rules;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>
/// Public bilingual news ("Báo song ngữ"): article list and detail, dictation grading (recorded for a signed-in
/// caller), guest word lookup and the cover images. A token, when sent, personalises the list.
/// </summary>
[ApiController]
[Route("api/v1/news")]
[AllowAnonymous]
public sealed class NewsController(ISender sender, INewsCoverStore covers) : ControllerBase
{
    /// <summary>Request path of the covers (excluded from the API no-store header in Program.cs).</summary>
    public const string CoversPath = "/api/v1/news/covers";

    private Guid OptionalLearnerId =>
        Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id) ? id : Guid.Empty;

    [HttpGet("articles")]
    [EnableRateLimiting(NewsRateLimits.Read)]
    public Task<NewsArticleListDto> Articles([FromQuery] string? stage, [FromQuery] string? topic,
        [FromQuery] string? q, [FromQuery] string? length, [FromQuery] string? state, [FromQuery] int page = 1,
        [FromQuery] int pageSize = NewsCatalog.MaxPageSize, CancellationToken ct = default) =>
        sender.Send(new GetNewsArticlesQuery(stage, topic, q, length, state, page, pageSize, OptionalLearnerId), ct);

    [HttpGet("articles/{slug}")]
    [EnableRateLimiting(NewsRateLimits.Read)]
    public Task<NewsArticleDetailDto> Article(string slug, CancellationToken ct) =>
        sender.Send(new GetNewsArticleQuery(slug), ct);

    [HttpPost("articles/{slug}/dictation")]
    [EnableRateLimiting(NewsRateLimits.Write)]
    public Task<DictationResultDto> Dictation(string slug, [FromBody] CheckDictationRequest body,
        CancellationToken ct) =>
        sender.Send(new CheckNewsDictationCommand(slug, body.SentenceId, body.Text, body.Revealed, OptionalLearnerId),
            ct);

    /// <summary>Fast-tier lookup for guests (no AI); signed-in readers use <c>vocabulary/lookup</c>.</summary>
    [HttpPost("lookup")]
    [EnableRateLimiting(NewsRateLimits.Lookup)]
    public Task<WordLookupDto> Lookup([FromBody] LookupNewsWordQuery query, CancellationToken ct) =>
        sender.Send(query, ct);

    /// <summary>A content-addressed cover image, cacheable forever.</summary>
    [HttpGet("covers/{file:regex(^[[0-9a-f]]{{16,64}}\\.webp$)}")]
    public IActionResult Cover(string file)
    {
        var path = covers.GetPhysicalPath(file) ?? throw NewsErrors.CoverNotFound();
        Response.Headers[HeaderNames.CacheControl] = "public, max-age=31536000, immutable";
        return PhysicalFile(path, "image/webp", enableRangeProcessing: false);
    }
}
