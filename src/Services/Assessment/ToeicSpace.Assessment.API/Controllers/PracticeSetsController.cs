using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.API.Contracts;
using ToeicSpace.Assessment.API.Security;
using ToeicSpace.Assessment.Application.Practice.Commands.CreatePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.DeletePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.ReplacePracticeSetItems;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSet;
using ToeicSpace.Assessment.Application.Practice.Commands.UpdatePracticeSetStatus;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetById;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSetContent;
using ToeicSpace.Assessment.Application.Practice.Queries.GetPracticeSets;

namespace ToeicSpace.Assessment.API.Controllers;

[ApiController]
[Route("api/v1/practice-sets")]
[Produces("application/json")]
public sealed class PracticeSetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public PracticeSetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "List practice sets",
        Description = "Part practice collections by level, topic or mixed drill. Learners only see published sets.")]
    [ProducesResponseType(typeof(PaginatedResult<PracticeSetDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetPracticeSetsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Get practice set by ID",
        Description = "Practice set metadata with its ordered question IDs.")]
    [ProducesResponseType(typeof(PracticeSetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPracticeSetByIdQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}/content")]
    [SwaggerOperation(
        Summary = "Get practice content",
        Description = "Passages and questions of the set in order, including answers and explanations for instant feedback.")]
    [ProducesResponseType(typeof(PracticeSetContentDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetContent(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPracticeSetContentQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Create a practice set",
        Description = "Creates an empty practice set in Draft status.")]
    [ProducesResponseType(typeof(PracticeSetDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePracticeSetCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Update a practice set",
        Description = "Updates practice set metadata.")]
    [ProducesResponseType(typeof(PracticeSetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdatePracticeSetCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { Id = id }, cancellationToken);

        return Ok(result);
    }

    [HttpPut("{id:guid}/items")]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Replace practice set questions",
        Description = "Replaces the ordered list of questions. All questions must belong to the set's part.")]
    [ProducesResponseType(typeof(PracticeSetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReplaceItems(
        [FromRoute] Guid id,
        [FromBody] ReplacePracticeSetItemsRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new ReplacePracticeSetItemsCommand(id, request.QuestionIds), cancellationToken);

        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Update practice set status",
        Description = "Publishing (Active) requires at least one Active question of the set's part.")]
    [ProducesResponseType(typeof(PracticeSetDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdatePracticeSetStatusCommand(id, request.Status), cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Delete a practice set",
        Description = "Soft deletes the practice set. Its questions stay in the bank.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeletePracticeSetCommand(id), cancellationToken);

        return NoContent();
    }
}
