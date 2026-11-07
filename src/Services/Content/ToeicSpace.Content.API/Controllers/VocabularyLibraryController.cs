using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.Common.Pagination;
using ToeicSpace.Content.Application.DTOs.VocabularyLibrary;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateCustomWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.CreateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.DeleteFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.RemoveSavedWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.SaveWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateFolder;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdatePersonalWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Commands.UpdateSavedWord;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetLibrary;
using ToeicSpace.Content.Application.Features.VocabularyLibrary.Queries.GetSavedWords;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>"Từ vựng của tôi": the signed-in learner's folders, saved official words and custom words.</summary>
[ApiController]
[Route("api/v1/vocabulary/me")]
[Authorize]
public sealed class VocabularyLibraryController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet("library")]
    public Task<LibraryDto> Library([FromQuery] int tzOffset = 0, CancellationToken ct = default) =>
        sender.Send(new GetLibraryQuery(LearnerId, tzOffset), ct);

    [HttpPost("folders")]
    public async Task<ActionResult<FolderDto>> CreateFolder([FromBody] CreateFolderCommand command,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await sender.Send(command with { LearnerId = LearnerId }, ct));

    [HttpPut("folders/{folderId:guid}")]
    public Task<FolderDto> UpdateFolder(Guid folderId, [FromBody] UpdateFolderCommand command, CancellationToken ct) =>
        sender.Send(command with { FolderId = folderId, LearnerId = LearnerId }, ct);

    [HttpDelete("folders/{folderId:guid}")]
    public async Task<IActionResult> DeleteFolder(Guid folderId, CancellationToken ct)
    {
        await sender.Send(new DeleteFolderCommand(folderId, LearnerId), ct);
        return NoContent();
    }

    [HttpGet("words")]
    public Task<PagedResult<SavedWordDto>> Words([FromQuery] Guid? folderId, [FromQuery] bool? unfiled,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        [FromQuery] int tzOffset = 0, CancellationToken ct = default) =>
        sender.Send(new GetSavedWordsQuery(LearnerId, folderId, unfiled, search, page, pageSize, tzOffset), ct);

    /// <summary>Saves an official word: 201 when added, 200 when it was already in the library.</summary>
    [HttpPost("words")]
    public async Task<ActionResult<SavedWordDto>> SaveWord([FromBody] SaveWordCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command with { LearnerId = LearnerId }, ct);
        return result.Created ? StatusCode(StatusCodes.Status201Created, result.SavedWord) : Ok(result.SavedWord);
    }

    [HttpPost("words/custom")]
    public async Task<ActionResult<SavedWordDto>> CreateCustomWord([FromBody] CreateCustomWordCommand command,
        CancellationToken ct) =>
        StatusCode(StatusCodes.Status201Created, await sender.Send(command with { LearnerId = LearnerId }, ct));

    /// <summary>
    /// The learner's own edit of a word (official: personal overrides, custom: the word itself); 201 when the edit
    /// added the word to the library, else 200.
    /// </summary>
    [HttpPut("word-edits/{wordId:guid}")]
    public async Task<ActionResult<SavedWordDto>> UpdatePersonalWord(Guid wordId,
        [FromBody] UpdatePersonalWordCommand command, CancellationToken ct)
    {
        var result = await sender.Send(command with { WordId = wordId, LearnerId = LearnerId }, ct);
        return result.Created ? StatusCode(StatusCodes.Status201Created, result.SavedWord) : Ok(result.SavedWord);
    }

    [HttpPut("words/{savedWordId:guid}")]
    public Task<SavedWordDto> UpdateWord(Guid savedWordId, [FromBody] UpdateSavedWordCommand command,
        CancellationToken ct) =>
        sender.Send(command with { SavedWordId = savedWordId, LearnerId = LearnerId }, ct);

    [HttpDelete("words/{savedWordId:guid}")]
    public async Task<IActionResult> RemoveWord(Guid savedWordId, CancellationToken ct)
    {
        await sender.Send(new RemoveSavedWordCommand(savedWordId, LearnerId), ct);
        return NoContent();
    }
}
