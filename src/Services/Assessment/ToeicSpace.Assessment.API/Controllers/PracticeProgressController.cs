using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.Application.DTOs.Attempts;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetQuestionStatuses;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeSetsProgress;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetPracticeStats;
using ToeicSpace.Assessment.Application.Features.Attempts.Queries.GetTestsProgress;

namespace ToeicSpace.Assessment.API.Controllers;

/// <summary>
/// The signed-in learner's practice progress, computed from their attempts.
/// </summary>
[ApiController]
[Authorize(Roles = "User")]
[Produces("application/json")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PracticeProgressController : ControllerBase
{
    private readonly IMediator _mediator;

    public PracticeProgressController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("api/v1/me/practice/tests-progress")]
    [SwaggerOperation(Summary = "Progress on the tests I have started")]
    [ProducesResponseType(typeof(IReadOnlyList<TestProgressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> TestsProgress(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetTestsProgressQuery(), cancellationToken));

    [HttpGet("api/v1/me/practice/sets-progress")]
    [SwaggerOperation(Summary = "Progress on published practice sets", Description = "Optional part filter (1–7).")]
    [ProducesResponseType(typeof(IReadOnlyList<PracticeSetProgressDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> SetsProgress(
        [FromQuery] int? part,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPracticeSetsProgressQuery(part), cancellationToken));

    [HttpGet("api/v1/me/practice/stats")]
    [SwaggerOperation(Summary = "Practice statistics", Description = "Totals, accuracy per part, FullTest score trend and recent attempts.")]
    [ProducesResponseType(typeof(PracticeStatsDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> Stats(CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPracticeStatsQuery(), cancellationToken));

    [HttpGet("api/v1/practice-sets/{id:guid}/my-questions")]
    [SwaggerOperation(Summary = "My status on each question of a practice set")]
    [ProducesResponseType(typeof(IReadOnlyList<PracticeQuestionStatusDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MyQuestions(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
        => Ok(await _mediator.Send(new GetPracticeSetQuestionStatusesQuery(id), cancellationToken));
}
