using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.LibraryImport;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.DTOs.VocabularyLookup;
using ToeicSpace.Content.Application.Features.LibraryImport.Commands.ImportWords;
using ToeicSpace.Content.Application.Features.LibraryImport.Commands.ParseImport;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.AiFillWord;
using ToeicSpace.Content.Application.Features.VocabularyLookup.Queries.LookupWord;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>"Thêm từ vựng": word lookup with enrichment, list/JSON import and AI fill (signed-in, rate limited).</summary>
[ApiController]
[Route("api/v1/vocabulary")]
[Authorize]
public sealed class VocabularyEnrichmentController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost("lookup")]
    [EnableRateLimiting(VocabularyRateLimits.Lookup)]
    public Task<WordLookupDto> Lookup([FromBody] LookupWordQuery query, CancellationToken ct) =>
        sender.Send(query with { LearnerId = LearnerId }, ct);

    [HttpPost("me/words/import/parse")]
    [EnableRateLimiting(VocabularyRateLimits.ImportParse)]
    public Task<ImportPreviewDto> ParseImport([FromBody] ParseImportCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);

    [HttpPost("me/words/import")]
    [EnableRateLimiting(VocabularyRateLimits.Import)]
    public Task<ImportResultDto> Import([FromBody] ImportWordsCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);

    [HttpPost("me/words/{savedWordId:guid}/ai-fill")]
    [EnableRateLimiting(VocabularyRateLimits.AiFill)]
    public Task<AiFillResultDto> AiFill(Guid savedWordId, [FromQuery] int tzOffset = 0, CancellationToken ct = default) =>
        sender.Send(new AiFillWordCommand(savedWordId, LearnerId, tzOffset), ct);
}
