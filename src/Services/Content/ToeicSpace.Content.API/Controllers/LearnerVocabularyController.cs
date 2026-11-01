using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.Application.DTOs.Vocabulary;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Commands.SubmitReview;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetLearnerSummary;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStageSetProgress;
using ToeicSpace.Content.Application.Features.VocabularyProgress.Queries.GetStudyQueue;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>The signed-in learner's vocabulary progress (any role); the learner is always the token subject.</summary>
[ApiController]
[Route("api/v1/vocabulary/me")]
[Authorize]
public sealed class LearnerVocabularyController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpGet("summary")]
    public Task<LearnerSummaryDto> Summary([FromQuery] int tzOffset = 0, CancellationToken ct = default) =>
        sender.Send(new GetLearnerSummaryQuery(LearnerId, tzOffset), ct);

    [HttpGet("stages/{stageId:guid}/sets")]
    public Task<IReadOnlyList<SetProgressDto>> StageSets(Guid stageId, [FromQuery] int tzOffset = 0,
        CancellationToken ct = default) =>
        sender.Send(new GetStageSetProgressQuery(LearnerId, stageId, tzOffset), ct);

    [HttpGet("study-queue")]
    public Task<IReadOnlyList<StudyItemDto>> StudyQueue([FromQuery] Guid? setId, [FromQuery] bool? due,
        [FromQuery] Guid? folderId, [FromQuery] bool? library,
        [FromQuery] int limit = GetStudyQueueQuery.DefaultLimit, [FromQuery] int tzOffset = 0,
        CancellationToken ct = default) =>
        sender.Send(new GetStudyQueueQuery(LearnerId, setId, due, limit, tzOffset, folderId, library), ct);

    [HttpPost("reviews")]
    public Task<ReviewResultDto> Review([FromBody] SubmitReviewCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);
}
