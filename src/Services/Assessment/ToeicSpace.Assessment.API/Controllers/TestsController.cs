using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.API.Contracts;
using ToeicSpace.Assessment.API.Security;
using ToeicSpace.Assessment.Application.Tests.Commands.CreateTest;
using ToeicSpace.Assessment.Application.Tests.Commands.DeleteTest;
using ToeicSpace.Assessment.Application.Tests.Commands.UpdateTest;
using ToeicSpace.Assessment.Application.Tests.Commands.UpdateTestStatus;
using ToeicSpace.Assessment.Application.Tests.Queries.GetFullTest;
using ToeicSpace.Assessment.Application.Tests.Queries.GetTestById;
using ToeicSpace.Assessment.Application.Tests.Queries.GetTestCategories;
using ToeicSpace.Assessment.Application.Tests.Queries.GetTests;

namespace ToeicSpace.Assessment.API.Controllers;

[ApiController]
[Route("api/v1/tests")]
[Produces("application/json")]
public sealed class TestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public TestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "List full tests",
        Description = "Paginated list of full TOEIC tests. Learners only see published tests; content managers can filter by status.")]
    [ProducesResponseType(typeof(PaginatedResult<TestDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAll(
        [FromQuery] GetTestsQuery query,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(query, cancellationToken);

        return Ok(result);
    }

    [HttpGet("categories")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Get test categories",
        Description = "Test series (e.g. ETS 2026, Crack TOEIC Vol 1) with their test counts and years.")]
    [ProducesResponseType(typeof(IReadOnlyList<TestCategorySummaryDto>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetCategories(
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTestCategoriesQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [AllowAnonymous]
    [SwaggerOperation(
        Summary = "Get test overview",
        Description = "Test metadata with actual question counts per part.")]
    [ProducesResponseType(typeof(TestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetTestByIdQuery(id), cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}/full")]
    [SwaggerOperation(
        Summary = "Get full test content",
        Description = "Parts 1-7 with passages and questions for taking the test. Answer keys, explanations and transcripts are hidden unless includeAnswers=true, which requires a content manager.")]
    [ProducesResponseType(typeof(FullTestDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetFullTest(
        [FromRoute] Guid id,
        [FromQuery] bool includeAnswers = false,
        CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetFullTestQuery(id, includeAnswers), cancellationToken);

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Create a test",
        Description = "Creates an empty full test in Draft status.")]
    [ProducesResponseType(typeof(TestDetailDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Create(
        [FromBody] CreateTestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command, cancellationToken);

        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Update a test",
        Description = "Updates test metadata.")]
    [ProducesResponseType(typeof(TestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Update(
        [FromRoute] Guid id,
        [FromBody] UpdateTestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(command with { Id = id }, cancellationToken);

        return Ok(result);
    }

    [HttpPatch("{id:guid}/status")]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Update test status",
        Description = "Publishing (Active) requires the standard ETS structure: 200 active questions with correct part counts and numbering.")]
    [ProducesResponseType(typeof(TestDetailDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> UpdateStatus(
        [FromRoute] Guid id,
        [FromBody] UpdateStatusRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new UpdateTestStatusCommand(id, request.Status), cancellationToken);

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = AuthorizationPolicies.ContentManager)]
    [SwaggerOperation(
        Summary = "Delete a test",
        Description = "Soft deletes the test. Questions used by practice sets stay in the question bank.")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Delete(
        [FromRoute] Guid id,
        CancellationToken cancellationToken)
    {
        await _mediator.Send(new DeleteTestCommand(id), cancellationToken);

        return NoContent();
    }
}
