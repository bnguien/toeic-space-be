using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.API.Security;
using ToeicSpace.Assessment.Application.Passages.Commands.CreatePassage;
using ToeicSpace.Assessment.Application.Passages.Commands.DeletePassage;
using ToeicSpace.Assessment.Application.Passages.Commands.UpdatePassage;
using ToeicSpace.Assessment.Application.Passages.Queries.GetPassageById;
using ToeicSpace.Assessment.Application.Passages.Queries.GetPassages;

namespace ToeicSpace.Assessment.API.Controllers;

[ApiController]
[Route("api/v1/passages")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.ContentManager)]
public sealed class PassagesController : ControllerBase
{
    private readonly IMediator _mediator;

    public PassagesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "List passages",
        Description = "Question groups (Part 3/4 conversations and talks, Part 6/7 texts) filtered by test and part.")]
    [ProducesResponseType(typeof(PaginatedResult<PassageDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetPassagesQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get passage by ID",
        Description = "Passage content with its questions.")]
    [ProducesResponseType(typeof(PassageDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetPassageByIdQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a passage",
        Description = "Creates a question group for Part 3, 4, 6 or 7.")]
    [ProducesResponseType(typeof(PassageDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Create(
        [FromBody] CreatePassageCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [SwaggerOperation(
        Summary = "Update a passage",
        Description = "Updates passage content. The test and part cannot change while it has questions.")]
    [ProducesResponseType(typeof(PassageDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdatePassageCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { Id = id }, cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [SwaggerOperation(
        Summary = "Delete a passage",
        Description = "Soft deletes a passage that no longer has questions.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeletePassageCommand(id), cancellationToken);

        return NoContent();
    }
}
