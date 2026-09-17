using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.API.Contracts;
using ToeicSpace.Assessment.API.Security;
using ToeicSpace.Assessment.Application.Questions.Commands.CreateQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.DeleteQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestion;
using ToeicSpace.Assessment.Application.Questions.Commands.UpdateQuestionStatus;
using ToeicSpace.Assessment.Application.Questions.Queries.GetQuestionById;
using ToeicSpace.Assessment.Application.Questions.Queries.GetQuestions;

namespace ToeicSpace.Assessment.API.Controllers;

[ApiController]
[Route("api/v1/questions")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.ContentManager)]
public sealed class QuestionsController : ControllerBase
{
    private readonly IMediator _mediator;

    public QuestionsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [SwaggerOperation(
        Summary = "List questions",
        Description = "Question bank with filters by test, passage, practice set, part, difficulty, status and full-text search.")]
    [ProducesResponseType(typeof(PaginatedResult<QuestionDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetQuestionsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [SwaggerOperation(
        Summary = "Get question by ID",
        Description = "Question detail including answer key, explanation, transcript and the practice sets using it.")]
    [ProducesResponseType(typeof(QuestionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetQuestionByIdQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [SwaggerOperation(
        Summary = "Create a question",
        Description = "Creates a question validated against ETS part rules. Test questions need a question number in the standard range of their part.")]
    [ProducesResponseType(typeof(QuestionDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [SwaggerOperation(
        Summary = "Update a question",
        Description = "Updates question content. Send the loaded version as expectedVersion; a newer version returns 409.")]
    [ProducesResponseType(typeof(QuestionDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateQuestionCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { Id = id }, cancellationToken);

        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [SwaggerOperation(
        Summary = "Update question status",
        Description = "Changes the lifecycle status (Draft, Active, Archived).")]
    [ProducesResponseType(typeof(QuestionDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateQuestionStatusCommand(id, request.Status), cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [SwaggerOperation(
        Summary = "Delete a question",
        Description = "Soft deletes the question and removes it from practice sets.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteQuestionCommand(id), cancellationToken);

        return NoContent();
    }
}
