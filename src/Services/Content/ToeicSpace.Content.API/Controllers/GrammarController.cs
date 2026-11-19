using System.Text.Json.Nodes;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ToeicSpace.BuildingBlocks.Security.Jwt;
using ToeicSpace.Content.API.RateLimiting;
using ToeicSpace.Content.Application.DTOs.Grammar;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.AnswerQuestion;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RetryAttemptAnswer;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RevealAttemptAnswer;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.RevealQuestion;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitStageAttempt;
using ToeicSpace.Content.Application.Features.GrammarAttempts.Commands.SubmitTopicAttempt;
using ToeicSpace.Content.Application.Features.GrammarContent.Queries.GetGrammarStageTest;
using ToeicSpace.Content.Application.Features.GrammarContent.Queries.GetGrammarTopic;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>
/// Public grammar roadmap endpoints: content without answers and server-side grading. Guests are graded but nothing is
/// stored; a token (if any) identifies the learner whose progress is saved.
/// </summary>
[ApiController]
[Route("api/v1/grammar")]
[AllowAnonymous]
public sealed class GrammarController(ISender sender) : ControllerBase
{
    private const string LessonCacheControl = "public, max-age=300";

    private Guid? LearnerId => User.Identity?.IsAuthenticated == true
        && Guid.TryParse(User.FindFirst(AccessTokenDefaults.SubjectClaim)?.Value, out var id) ? id : null;

    /// <summary>Knowledge + vocabulary + exercises of a topic without answers. ETag = content version → 304.</summary>
    [HttpGet("topics/{topic:regex(^\\d{{2}}$)}")]
    public async Task<IActionResult> Topic(string topic, CancellationToken ct)
    {
        var payload = await sender.Send(new GetGrammarTopicQuery(topic), ct);
        var etag = $"\"{payload.Version}\"";
        Response.Headers.ETag = etag;
        Response.Headers.CacheControl = LessonCacheControl;
        var match = Request.Headers.IfNoneMatch.ToString().Split(',').Select(t => t.Trim()).Contains(etag);
        return match ? StatusCode(StatusCodes.Status304NotModified) : File(payload.Json, "application/json; charset=utf-8");
    }

    /// <summary>A stage test without answers + the viewer's <c>status</c> (guests: locked).</summary>
    [HttpGet("stages/{stage:regex(^\\d{{2}}$)}/test")]
    public async Task<JsonObject> StageTest(string stage, CancellationToken ct)
    {
        Response.Headers.CacheControl = "private, no-store";
        return await sender.Send(new GetGrammarStageTestQuery(stage, LearnerId), ct);
    }

    [HttpPost("topics/{topic:regex(^\\d{{2}}$)}/attempts")]
    [EnableRateLimiting(GrammarRateLimits.Submit)]
    public Task<AttemptResultDto> SubmitTopic(string topic, [FromBody] SubmitTopicAttemptCommand command,
        CancellationToken ct) =>
        sender.Send(command with { Topic = topic, LearnerId = LearnerId }, ct);

    [HttpPost("stages/{stage:regex(^\\d{{2}}$)}/attempts")]
    [EnableRateLimiting(GrammarRateLimits.Submit)]
    public Task<AttemptResultDto> SubmitStage(string stage, [FromBody] SubmitStageAttemptCommand command,
        CancellationToken ct) =>
        sender.Send(command with { Stage = stage, LearnerId = LearnerId }, ct);

    [HttpPost("attempts/{attemptId}/retry")]
    [EnableRateLimiting(GrammarRateLimits.Answer)]
    public Task<AnswerResultDto> Retry(string attemptId, [FromBody] RetryAttemptAnswerCommand command, CancellationToken ct) =>
        sender.Send(command with { AttemptId = attemptId, LearnerId = LearnerId }, ct);

    [HttpPost("attempts/{attemptId}/reveal")]
    [EnableRateLimiting(GrammarRateLimits.Reveal)]
    public Task<JsonObject> Reveal(string attemptId, [FromBody] RevealAttemptAnswerCommand command, CancellationToken ct) =>
        sender.Send(command with { AttemptId = attemptId, LearnerId = LearnerId }, ct);

    /// <summary>Single question (mistake book): graded like a retry, no attempt is created.</summary>
    [HttpPost("questions/{questionId:regex(^[[A-Za-z0-9_.-]]{{1,40}}$)}/answer")]
    [EnableRateLimiting(GrammarRateLimits.Answer)]
    public Task<AnswerResultDto> Answer(string questionId, [FromBody] AnswerQuestionCommand command, CancellationToken ct) =>
        sender.Send(command with { QuestionId = questionId, LearnerId = LearnerId }, ct);

    [HttpPost("questions/{questionId:regex(^[[A-Za-z0-9_.-]]{{1,40}}$)}/reveal")]
    [EnableRateLimiting(GrammarRateLimits.Reveal)]
    public Task<JsonObject> RevealQuestion(string questionId, CancellationToken ct) =>
        sender.Send(new RevealQuestionCommand(questionId, LearnerId), ct);
}
