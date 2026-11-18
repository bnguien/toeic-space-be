using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Content.API.Security;
using ToeicSpace.Content.Application.Analytics.Queries.GetAnalyticsCatalog;
using ToeicSpace.Content.Application.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Content.Application.DTOs.Analytics;

namespace ToeicSpace.Content.API.Controllers;

/// <summary>
/// Read-only learning analytics of the Content service for admins and the AI data analyst. Metrics come from a
/// fixed catalogue; outputs never contain names, emails or raw user IDs.
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
    /// Every metric with its unit, allowed dimensions (by) and filters, visibility label and whether small groups
    /// are suppressed; plus dimension and filter descriptions, limits and planned metrics.
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
    /// Groups of learner metrics with fewer than 5 distinct learners return value null and n 0 and are counted in
    /// 'suppressed'. Unknown metrics, dimensions, filters or values return 400 with the allowed values.
    /// </summary>
    /// <param name="metric">Metric name from the catalogue, e.g. study_time, grammar_accuracy, news_reads.</param>
    /// <param name="by">Comma-separated dimensions (max 2) allowed for the metric, e.g. week or month,course.</param>
    /// <param name="from">Start date yyyy-MM-dd (UTC, inclusive). Default: to minus 89 days.</param>
    /// <param name="to">End date yyyy-MM-dd (UTC, inclusive). Default: today. Range at most 366 days.</param>
    /// <param name="status">Course status (Draft, Published, Archived) or lesson status (Draft, Published).</param>
    /// <param name="level">Course level: Beginner, Intermediate or Advanced.</param>
    /// <param name="courseId">Course GUID (lesson_count) or dictation course ID (dictation metrics).</param>
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
        [FromQuery] string? level,
        [FromQuery(Name = "course_id")] string? courseId,
        [FromQuery] int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new QueryAnalyticsQuery(metric, by, from, to, status, level, courseId, limit),
            cancellationToken);

        return Ok(result);
    }
}
