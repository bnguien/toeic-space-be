using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Classroom.API.Security;
using ToeicSpace.Classroom.Application.Analytics.Queries.GetAnalyticsCatalog;
using ToeicSpace.Classroom.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Classroom.Application.DTOs.Analytics;

namespace ToeicSpace.Classroom.API.Controllers;

/// <summary>
/// Read-only class and enrollment analytics for admins and the AI data analyst. Metrics come from a fixed
/// catalogue; outputs never contain names, emails or raw user IDs (teachers appear only as an opaque hash).
/// </summary>
[ApiController]
[Route("api/v1/analytics")]
[Produces("application/json")]
[Authorize(Policy = AuthorizationPolicies.AnalyticsReader)]
public sealed class AnalyticsController : ControllerBase
{
    private readonly ISender _sender;

    public AnalyticsController(ISender sender)
    {
        _sender = sender;
    }

    /// <summary>
    /// Every metric with its unit, allowed dimensions (by) and filters, visibility label and whether it is
    /// computed per learner; plus dimension and filter descriptions, limits and planned metrics.
    /// </summary>
    [HttpGet("catalog")]
    [ProducesResponseType(typeof(AnalyticsCatalogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAnalyticsCatalogQuery(), cancellationToken);

        return Ok(result);
    }

    /// <summary>
    /// Computes one catalogue metric grouped by up to two dimensions. Rows hold each dimension, value and n.
    /// Groups of learner metrics with fewer than 5 distinct learners return value null and n 0 and are counted
    /// in 'suppressed'. Unknown metrics, dimensions, filters or values return 400 with the allowed values.
    /// </summary>
    /// <param name="metric">Metric name from the catalogue, e.g. class_count, class_enrollment, withdraw_rate.</param>
    /// <param name="by">Comma-separated dimensions (max 2) allowed for the metric, e.g. month or month,status.</param>
    /// <param name="from">Start date yyyy-MM-dd (UTC, inclusive). Default: to minus 89 days.</param>
    /// <param name="to">End date yyyy-MM-dd (UTC, inclusive). Default: today. Range at most 366 days.</param>
    /// <param name="status">Class status (Draft, Active, Completed, Archived) or enrollment status (Active, Withdrawn), per metric.</param>
    /// <param name="classId">Only this class (GUID).</param>
    /// <param name="limit">Maximum rows, 1-500. Default 100.</param>
    /// <param name="cancellationToken">Request cancellation.</param>
    [HttpGet("query")]
    [ProducesResponseType(typeof(AnalyticsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Query(
        [FromQuery] string? metric,
        [FromQuery] string? by,
        [FromQuery] string? from,
        [FromQuery] string? to,
        [FromQuery] string? status,
        [FromQuery(Name = "class_id")] string? classId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new QueryAnalyticsQuery(metric, by, from, to, status, classId, limit),
            cancellationToken);

        return Ok(result);
    }
}
