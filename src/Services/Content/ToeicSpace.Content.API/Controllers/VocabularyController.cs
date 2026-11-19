using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.ReelBackgrounds;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Features.ReelBackgrounds.Queries.GetReelBackgrounds;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetSetDetail;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStages;
using ToeicSpace.Content.Application.Features.VocabularyCatalog.Queries.GetStageSets;
using ToeicSpace.Content.Application.Features.VocabularyQuizzes.Commands.CreateQuiz;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Public vocabulary catalog, quiz builder and Reel backgrounds.</summary>
[ApiController]
[Route("api/v1/vocabulary")]
[AllowAnonymous]
public sealed class VocabularyController(ISender sender) : ControllerBase
{
    [HttpGet("stages")]
    public Task<IReadOnlyList<StageDto>> Stages(CancellationToken ct) => sender.Send(new GetStagesQuery(), ct);

    [HttpGet("stages/{stageId:guid}/sets")]
    public Task<IReadOnlyList<SetSummaryDto>> StageSets(Guid stageId, CancellationToken ct) =>
        sender.Send(new GetStageSetsQuery(stageId), ct);

    /// <summary>A signed-in caller sees the words with their personal edits.</summary>
    [HttpGet("sets/{setId:guid}")]
    public Task<SetDetailDto> Set(Guid setId, CancellationToken ct) =>
        sender.Send(new GetSetDetailQuery(setId, OptionalLearnerId), ct);

    /// <summary>Guests get official words only; a signed-in caller may also quiz their own custom words.</summary>
    [HttpPost("quizzes")]
    public Task<IReadOnlyList<QuizQuestionDto>> Quiz([FromBody] CreateQuizCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = OptionalLearnerId }, ct);

    private Guid OptionalLearnerId =>
        Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id) ? id : Guid.Empty;

    [HttpGet("reel-backgrounds")]
    public Task<IReadOnlyList<ReelBackgroundDto>> ReelBackgrounds(
        [FromQuery] int count = GetReelBackgroundsQuery.DefaultCount, CancellationToken ct = default) =>
        sender.Send(new GetReelBackgroundsQuery(count), ct);
}
