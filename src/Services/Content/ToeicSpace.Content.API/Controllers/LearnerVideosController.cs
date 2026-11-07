using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.Videos;
using ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckCloze;
using ToeicSpace.Content.Application.Features.VideoExercises.Queries.CheckRewrite;
using ToeicSpace.Content.Application.Features.VideoLearnerStates.Commands.UpdateVideoLearnerState;
using ToeicSpace.Content.Application.Features.VideoLearnerStates.Queries.GetVideoLearnerState;
using ToeicSpace.Content.Application.Features.VideoSubtitles.Commands.ResolveVideo;
using ToeicSpace.Content.Application.Features.VideoSubtitles.Queries.GetVideoSubtitles;
using ToeicSpace.Content.Application.Features.VideoWatches.Commands.RemoveMyVideo;
using ToeicSpace.Content.Application.Features.VideoWatches.Commands.SaveWatchProgress;
using ToeicSpace.Content.Application.Features.VideoWatches.Commands.SetVideoPublic;
using ToeicSpace.Content.Application.Features.VideoWatches.Queries.GetMyVideos;
using ToeicSpace.Content.Application.Features.VideoWatches.Queries.GetWatchProgress;
using ToeicSpace.Content.Application.Features.VideoWords.Queries.GetVideoSavedWords;
using ToeicSpace.Content.Domain.Exceptions;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>Signed-in part of "Video song ngữ": opening videos, subtitles, my videos, progress, exercises, state.</summary>
[ApiController]
[Route("api/v1/videos")]
[Authorize]
public sealed class LearnerVideosController(ISender sender) : ControllerBase
{
    private Guid LearnerId => Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id)
        ? id : throw new ContentRuleException("invalid_subject", "Invalid user identity.", 401);

    [HttpPost("resolve")]
    [EnableRateLimiting(VideoRateLimits.Resolve)]
    public Task<VideoResolveDto> Resolve([FromBody] ResolveVideoCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);

    /// <summary>200 cues, or 202 <c>{status:"processing", retryAfter, videoId}</c> while they are prepared.</summary>
    [HttpGet("{videoId}/subs")]
    [EnableRateLimiting(VideoRateLimits.Subtitles)]
    public async Task<IActionResult> GetSubtitles(string videoId, [FromQuery] string? langs, CancellationToken ct)
    {
        var result = await sender.Send(new GetVideoSubtitlesQuery(videoId, langs, LearnerId), ct);
        return result.Processing is not null ? StatusCode(StatusCodes.Status202Accepted, result.Processing) : Ok(result.Subtitles);
    }

    [HttpGet("me")]
    public Task<MyVideosDto> GetMyVideos(CancellationToken ct) => sender.Send(new GetMyVideosQuery(LearnerId), ct);

    [HttpPatch("me/{id:guid}")]
    public Task<SetVideoPublicResultDto> SetPublic(Guid id, [FromBody] SetVideoPublicCommand command, CancellationToken ct) =>
        sender.Send(command with { Id = id, LearnerId = LearnerId }, ct);

    [HttpDelete("me/{id:guid}")]
    public async Task<IActionResult> RemoveMyVideo(Guid id, CancellationToken ct)
    {
        await sender.Send(new RemoveMyVideoCommand(id, LearnerId), ct);
        return NoContent();
    }

    [HttpGet("me/progress")]
    public Task<WatchProgressListDto> GetProgress(CancellationToken ct) => sender.Send(new GetWatchProgressQuery(LearnerId), ct);

    [HttpPut("me/progress/{videoId}")]
    [EnableRateLimiting(VideoRateLimits.Progress)]
    public Task<SaveProgressResultDto> SaveProgress(string videoId, [FromBody] SaveWatchProgressCommand command,
        CancellationToken ct) => sender.Send(command with { VideoId = videoId, LearnerId = LearnerId }, ct);

    [HttpGet("me/saved-words")]
    public Task<VideoSavedWordsDto> GetSavedWords(CancellationToken ct) =>
        sender.Send(new GetVideoSavedWordsQuery(LearnerId), ct);

    [HttpGet("me/state")]
    public Task<VideoLearnerStateDto> GetState(CancellationToken ct) =>
        sender.Send(new GetVideoLearnerStateQuery(LearnerId), ct);

    [HttpPut("me/state")]
    public Task<VideoLearnerStateDto> UpdateState([FromBody] UpdateVideoLearnerStateCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = LearnerId }, ct);

    [HttpPost("{videoId}/cloze/check")]
    [EnableRateLimiting(VideoRateLimits.Exercise)]
    public Task<ClozeCheckResultDto> CheckCloze(string videoId, [FromBody] CheckClozeQuery query, CancellationToken ct) =>
        sender.Send(query with { VideoId = videoId, LearnerId = LearnerId }, ct);

    [HttpPost("{videoId}/rewrite/check")]
    [EnableRateLimiting(VideoRateLimits.Exercise)]
    public Task<RewriteCheckResultDto> CheckRewrite(string videoId, [FromBody] CheckRewriteQuery query, CancellationToken ct) =>
        sender.Send(query with { VideoId = videoId, LearnerId = LearnerId }, ct);
}
