using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.API.Security;
using ToeicSpace.Assessment.Application.Bank.Queries.GetBankOverview;

namespace ToeicSpace.Assessment.API.Controllers;

[ApiController]
[Route("api/v1/bank")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.ContentManager)]
public sealed class BankController : ControllerBase
{
    private readonly IMediator _mediator;

    public BankController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("overview")]
    [SwaggerOperation(
        Summary = "Question bank overview",
        Description = "Counts per part, status, difficulty and practice set kind for the admin CMS.")]
    [ProducesResponseType(typeof(BankOverviewDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetOverview(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetBankOverviewQuery(), cancellationToken);

        return Ok(result);
    }
}
