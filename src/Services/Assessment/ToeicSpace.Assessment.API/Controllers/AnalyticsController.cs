using Microsoft.AspNetCore.Authorization;
using ToeicSpace.Assessment.API.Security;
using ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsCatalog;
using ToeicSpace.Assessment.Application.Analytics.Queries.GetAnalyticsEvents;
using ToeicSpace.Assessment.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Assessment.Application.Dtos;

namespace ToeicSpace.Assessment.API.Controllers;

/// <summary>
/// Read-only learning analytics for admins and the AI data analyst. Metrics come from a fixed catalogue;
/// outputs never contain names, emails or raw user IDs.
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AnalyticsReader)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly IMediator _mediator;

    public AnalyticsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("catalog")]
    [SwaggerOperation(
        Summary = "Analytics catalogue",
        Description = "Every metric with its unit, allowed dimensions (by) and filters, visibility label and whether it is "
            + "computed per learner; plus dimension and filter descriptions, event types, limits and planned metrics.")]
    [ProducesResponseType(typeof(AnalyticsCatalogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(new GetAnalyticsCatalogQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("query")]
    [SwaggerOperation(
        Summary = "Query a metric",
        Description = "Computes one catalogue metric grouped by up to two dimensions. Rows hold each dimension, value and n. "
            + "Groups of learner metrics with fewer than 5 distinct learners return value null and n 0 and are counted in "
            + "'suppressed'. Unknown metrics, dimensions, filters or values return 400 with the allowed values.")]
    [ProducesResponseType(typeof(AnalyticsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Query(
        [FromQuery, SwaggerParameter("Metric name from the catalogue, e.g. accuracy, avg_score, attempt_count.", Required = true)]
        string? metric,
        [FromQuery, SwaggerParameter("Comma-separated dimensions (max 2) allowed for the metric, e.g. part or month,mode.")]
        string? by,
        [FromQuery, SwaggerParameter("Start date yyyy-MM-dd (UTC, inclusive). Default: to minus 89 days.")]
        string? from,
        [FromQuery, SwaggerParameter("End date yyyy-MM-dd (UTC, inclusive). Default: today. Range at most 366 days.")]
        string? to,
        [FromQuery(Name = "test_id"), SwaggerParameter("Only this test (GUID).")]
        string? testId,
        [FromQuery, SwaggerParameter("TOEIC part: 1-7 or Part1-Part7.")]
        string? part,
        [FromQuery, SwaggerParameter("Listening or Reading.")]
        string? section,
        [FromQuery, SwaggerParameter("1-5 or VeryEasy, Easy, Medium, Hard, VeryHard.")]
        string? difficulty,
        [FromQuery, SwaggerParameter("Attempt status (InProgress, Completed, Abandoned) or, for bank metrics, content status (Draft, Active, Archived).")]
        string? status,
        [FromQuery, SwaggerParameter("Attempt mode: FullTest, Practice or PartPractice.")]
        string? mode,
        [FromQuery, SwaggerParameter("Maximum rows, 1-500. Default 100.")]
        int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new QueryAnalyticsQuery(metric, by, from, to, testId, part, section, difficulty, status, mode, limit),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet("events")]
    [SwaggerOperation(
        Summary = "Anonymised events",
        Description = "Attempt or answer events of attempts started in the range, ordered by start time, with cursor paging. "
            + "'learner' and 'attempt' are HMAC-SHA256 pseudonyms (16 hex characters), stable across calls. "
            + "Returns 503 when the pseudonym key is not configured.")]
    [ProducesResponseType(typeof(AnalyticsEventsDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetEvents(
        [FromQuery, SwaggerParameter("Event type: attempt or answer.", Required = true)]
        string? type,
        [FromQuery, SwaggerParameter("Start date yyyy-MM-dd (UTC, inclusive). Default: to minus 89 days.")]
        string? from,
        [FromQuery, SwaggerParameter("End date yyyy-MM-dd (UTC, inclusive). Default: today. Range at most 366 days.")]
        string? to,
        [FromQuery, SwaggerParameter("nextCursor of the previous page; omit for the first page.")]
        string? cursor,
        [FromQuery, SwaggerParameter("Maximum rows, 1-1000. Default 200.")]
        int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _mediator.Send(
            new GetAnalyticsEventsQuery(type, from, to, cursor, limit),
            cancellationToken);

        return Ok(result);
    }
}
