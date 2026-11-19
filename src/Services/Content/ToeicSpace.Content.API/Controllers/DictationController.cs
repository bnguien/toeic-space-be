using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.Middlewares;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.Dictation;
using ToeicSpace.Content.Application.DTOs.Speech;
using ToeicSpace.Content.Application.Features.Dictation.Commands.CheckDictationAnswer;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetCourseVocab;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationConfig;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationExercises;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationHint;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationRoadmap;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetDictationSummary;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseAudio;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetExerciseSkeleton;
using ToeicSpace.Content.Application.Features.Dictation.Queries.GetPackCourses;
using ToeicSpace.Content.Infrastructure.Services.Speech;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>
/// Dictation ("Nghe chép chính tả"): catalog, roadmap, play endpoints. Public; a bearer token adds the learner's
/// progress and saves submissions. Answers only leave the server through <c>check</c> (after submitting) and
/// <c>hint</c> (one word).
/// </summary>
[ApiController]
[Route("api/v1/dictation")]
[AllowAnonymous]
public sealed class DictationController(ISender sender, IOptions<SpeechOptions> speech) : ControllerBase
{
    private Guid? OptionalLearnerId =>
        Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id) ? id : null;

    [HttpGet("config")]
    public Task<DictationConfigDto> Config(CancellationToken ct) => sender.Send(new GetDictationConfigQuery(), ct);

    [HttpGet("roadmap")]
    public Task<DictationRoadmapDto> Roadmap(CancellationToken ct) =>
        sender.Send(new GetDictationRoadmapQuery(OptionalLearnerId), ct);

    [HttpGet("courses")]
    public Task<PackCoursesDto> Courses([FromQuery] string? pack, CancellationToken ct) =>
        sender.Send(new GetPackCoursesQuery(pack, OptionalLearnerId), ct);

    [HttpGet("courses/{courseId:int}/vocab")]
    [EnableRateLimiting(DictationRateLimits.Skeleton)]
    public Task<CourseVocabDto> CourseVocab(int courseId, CancellationToken ct) =>
        sender.Send(new GetCourseVocabQuery(courseId), ct);

    [HttpGet("exercises")]
    public Task<DictationExercisesDto> Exercises([FromQuery] int level = 1, [FromQuery] int? course = null,
        CancellationToken ct = default) =>
        sender.Send(new GetDictationExercisesQuery(level, course, OptionalLearnerId), ct);

    [HttpGet("exercises/{itemId:int}/skeleton")]
    [EnableRateLimiting(DictationRateLimits.Skeleton)]
    public Task<DictationSkeletonDto> Skeleton(int itemId, [FromQuery] string? voice, [FromQuery] string? next,
        CancellationToken ct) =>
        sender.Send(new GetExerciseSkeletonQuery(itemId, voice, next), ct);

    /// <summary>mp3 (audio/mpeg, cached 7 days); 503 <c>SPEECH_UNAVAILABLE</c> → the client uses speechSynthesis.</summary>
    [HttpGet("exercises/{itemId:int}/audio")]
    [EnableRateLimiting(DictationRateLimits.Audio)]
    public async Task<IActionResult> Audio(int itemId, [FromQuery] string? voice, [FromQuery] double? rate,
        CancellationToken ct) =>
        AudioFile(await sender.Send(new GetExerciseAudioQuery(itemId, voice, rate), ct));

    [HttpPost("hint")]
    [EnableRateLimiting(DictationRateLimits.Hint)]
    public Task<DictationHintDto> Hint([FromBody] GetDictationHintQuery query, CancellationToken ct) =>
        sender.Send(query, ct);

    [HttpPost("check")]
    [EnableRateLimiting(DictationRateLimits.Check)]
    public Task<DictationCheckDto> Check([FromBody] CheckDictationAnswerCommand command, CancellationToken ct) =>
        sender.Send(command with { LearnerId = OptionalLearnerId }, ct);

    [HttpGet("summary")]
    public Task<DictationSummaryDto> Summary(CancellationToken ct) =>
        sender.Send(new GetDictationSummaryQuery(OptionalLearnerId), ct);

    private PhysicalFileResult AudioFile(SpeechClipDto clip)
    {
        PublicCacheControl.Set(HttpContext, speech.Value.ClientCacheSeconds);
        Response.Headers.ContentDisposition = $"inline; filename=\"{clip.FileName}\"";
        return PhysicalFile(clip.FilePath, "audio/mpeg", enableRangeProcessing: true);
    }
}
