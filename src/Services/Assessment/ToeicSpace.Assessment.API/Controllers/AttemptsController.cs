using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.API.Contracts;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.CheckAnswers;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.DiscardAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.ResumeAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.RetakeAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.SaveAnswer;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.SetQuestionFlag;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartPracticeSetAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.StartTestAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Commands.SubmitAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActivePracticeSetAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetActiveTestAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempt;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptResult;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttemptReview;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetAttempts;

namespace ToeicSpace.Assessment.API.Controllers;

/// <summary>
/// Learner attempts on full tests (mock exam or part practice) and practice sets.
/// Answers are never part of the attempt document until they are checked (practice) or submitted.
/// </summary>
[ApiController]
[Authorize(Roles = "User")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttemptsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AttemptsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("api/v1/tests/{testId:guid}/attempts")]
    [SwaggerOperation(
        Summary = "Start or resume a test attempt",
        Description = "FullTest covers Parts 1–7 with a server deadline; Practice needs parts. An in-progress attempt is resumed unless restart is true.")]
    [ProducesResponseType(typeof(AttemptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartTest(
        [FromRoute] Guid testId,
        [FromBody] StartTestAttemptRequest body,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(
            new StartTestAttemptCommand(testId, body.Mode, body.Parts, body.Restart ?? false), cancellationToken));

    [HttpGet("api/v1/tests/{testId:guid}/attempts/active")]
    [SwaggerOperation(Summary = "Get the in-progress attempt on a test")]
    [ProducesResponseType(typeof(ActiveTestAttemptDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActiveTest(
        [FromRoute] Guid testId,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetActiveTestAttemptQuery(testId), cancellationToken));

    [HttpPost("api/v1/practice-sets/{id:guid}/attempts")]
    [SwaggerOperation(
        Summary = "Start or resume a practice set session",
        Description = "selection: All (default), Continue, Random or Mistakes; count is a soft target (passage groups stay whole).")]
    [ProducesResponseType(typeof(AttemptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> StartPracticeSet(
        [FromRoute] Guid id,
        [FromBody] StartPracticeSetAttemptRequest body,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new StartPracticeSetAttemptCommand(
            id,
            body.ExpectedGeneration,
            body.Selection ?? PracticeSelection.All,
            body.Count,
            body.Restart ?? false), cancellationToken));

    [HttpGet("api/v1/practice-sets/{id:guid}/attempts/active")]
    [SwaggerOperation(Summary = "Get the in-progress session on a practice set and its start generation")]
    [ProducesResponseType(typeof(ActivePracticeAttemptDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> ActivePracticeSet(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetActivePracticeSetAttemptQuery(id), cancellationToken));

    [HttpGet("api/v1/attempts")]
    [SwaggerOperation(Summary = "List my attempts", Description = "Newest first; filter by mode, kind (Test/PracticeSet), sourceId and status.")]
    [ProducesResponseType(typeof(PaginatedResult<AttemptSummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetAttemptsQuery query,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(query, cancellationToken));

    [HttpGet("api/v1/attempts/{id:guid}")]
    [SwaggerOperation(Summary = "Get an attempt", Description = "Reveal holds checked questions while in progress and everything once completed.")]
    [ProducesResponseType(typeof(AttemptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAttemptQuery(id), cancellationToken));

    [HttpPost("api/v1/attempts/{id:guid}/resume")]
    [SwaggerOperation(Summary = "Resume an in-progress attempt")]
    [ProducesResponseType(typeof(AttemptDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Resume(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new ResumeAttemptCommand(id), cancellationToken));

    [HttpPut("api/v1/attempts/{id:guid}/answers/{questionId:guid}")]
    [SwaggerOperation(Summary = "Save an answer", Description = "Optimistic revision check; mutationId makes retries idempotent.")]
    [ProducesResponseType(typeof(SavedAnswerDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> SaveAnswer(
        [FromRoute] Guid id,
        [FromRoute] Guid questionId,
        [FromBody] SaveAnswerRequest body,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new SaveAnswerCommand(
            id, questionId, body.Answer, body.ExpectedRevision, body.MutationId, body.TimeSpentSeconds), cancellationToken));

    [HttpPut("api/v1/attempts/{id:guid}/flags/{questionId:guid}")]
    [SwaggerOperation(Summary = "Flag or unflag a question")]
    [ProducesResponseType(typeof(QuestionFlagDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetFlag(
        [FromRoute] Guid id,
        [FromRoute] Guid questionId,
        [FromBody] SetQuestionFlagRequest body,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new SetQuestionFlagCommand(id, questionId, body.Flagged), cancellationToken));

    [HttpPost("api/v1/attempts/{id:guid}/check")]
    [SwaggerOperation(Summary = "Check practice answers", Description = "Practice only (FullTest → 403 check_not_allowed). Locks and reveals the questions.")]
    [ProducesResponseType(typeof(RevealDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Check(
        [FromRoute] Guid id,
        [FromBody] CheckAnswersRequest body,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new CheckAnswersCommand(id, body.QuestionIds ?? []), cancellationToken));

    [HttpPost("api/v1/attempts/{id:guid}/submit")]
    [SwaggerOperation(Summary = "Submit an attempt", Description = "Grades and completes the attempt; idempotent.")]
    [ProducesResponseType(typeof(AttemptResultDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Submit(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new SubmitAttemptCommand(id), cancellationToken));

    [HttpGet("api/v1/attempts/{id:guid}/result")]
    [SwaggerOperation(Summary = "Get the result of a completed attempt")]
    [ProducesResponseType(typeof(AttemptResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Result(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAttemptResultQuery(id), cancellationToken));

    [HttpGet("api/v1/attempts/{id:guid}/review")]
    [SwaggerOperation(Summary = "Review a completed attempt", Description = "The attempt document with everything revealed.")]
    [ProducesResponseType(typeof(AttemptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Review(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetAttemptReviewQuery(id), cancellationToken));

    [HttpPost("api/v1/attempts/{id:guid}/retake")]
    [SwaggerOperation(Summary = "Retake a completed attempt", Description = "New Practice attempt with the wrong/unanswered questions (Wrong) or all of them (All).")]
    [ProducesResponseType(typeof(AttemptDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Retake(
        [FromRoute] Guid id,
        [FromBody] RetakeAttemptRequest body,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new RetakeAttemptCommand(id, body.Scope), cancellationToken));

    [HttpDelete("api/v1/attempts/{id:guid}")]
    [SwaggerOperation(Summary = "Discard an in-progress attempt")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Discard(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DiscardAttemptCommand(id), cancellationToken);

        return NoContent();
    }
}
