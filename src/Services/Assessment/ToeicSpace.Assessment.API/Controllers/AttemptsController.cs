using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.Application.Attempts;

namespace ToeicSpace.Assessment.API.Controllers;

[ApiController]
[Authorize(Roles = "User")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttemptsController(IMediator mediator) : ControllerBase
{
    public sealed record StartRequest(long ExpectedGeneration);
    public sealed record AnswerRequest(AnswerOption? Answer, long ExpectedRevision, Guid MutationId);

    [HttpPost("api/v1/practice-sets/{id:guid}/attempts")]
    public async Task<IActionResult> Start(Guid id, StartRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new StartPracticeAttempt(id, body.ExpectedGeneration), ct));
    [HttpGet("api/v1/practice-sets/{id:guid}/attempts/active")]
    public async Task<IActionResult> Active(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetActivePracticeAttempt(id), ct));
    [HttpGet("api/v1/attempts/{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new GetPracticeAttempt(id), ct));
    [HttpPost("api/v1/attempts/{id:guid}/resume")]
    public async Task<IActionResult> Resume(Guid id, CancellationToken ct)
        => Ok(await mediator.Send(new ResumePracticeAttempt(id), ct));
    [HttpPut("api/v1/attempts/{id:guid}/answers/{questionId:guid}")]
    public async Task<IActionResult> Save(Guid id, Guid questionId, AnswerRequest body, CancellationToken ct)
        => Ok(await mediator.Send(new SavePracticeAnswer(id, questionId, body.Answer, body.ExpectedRevision, body.MutationId), ct));
    [HttpDelete("api/v1/attempts/{id:guid}")]
    public async Task<IActionResult> Discard(Guid id, CancellationToken ct)
    {
        await mediator.Send(new DiscardPracticeAttempt(id), ct);
        return NoContent();
    }
}
