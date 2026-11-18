using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using ToeicSpace.Identity.API.Security;
using ToeicSpace.Identity.Application.Features.Analytics.Queries.GetAnalyticsCatalog;
using ToeicSpace.Identity.Application.Features.Analytics.Queries.QueryAnalytics;
using ToeicSpace.Identity.Application.Features.Analytics.Results;

namespace ToeicSpace.Identity.API.Controllers;

/// <summary>
/// Read-only account analytics for admins and the AI data analyst. Metrics come from a fixed catalogue;
/// outputs never contain names, emails or user IDs.
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

    [HttpGet("catalog")]
    [SwaggerOperation(
        Summary = "Analytics catalogue",
        Description = "Every account metric with its unit, allowed dimensions (by) and filters, visibility label and "
            + "whether small groups are suppressed; plus dimension and filter descriptions and limits.")]
    [ProducesResponseType(typeof(AnalyticsCatalogDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetCatalog(CancellationToken cancellationToken)
    {
        var result = await _sender.Send(new GetAnalyticsCatalogQuery(), cancellationToken);

        return Ok(result);
    }

    [HttpGet("query")]
    [SwaggerOperation(
        Summary = "Query a metric",
        Description = "Computes one catalogue metric grouped by up to two dimensions. Rows hold each dimension, value and n. "
            + "Groups of sign-in metrics with fewer than 5 distinct accounts return value null and n 0 and are counted in "
            + "'suppressed'. Unknown metrics, dimensions, filters or values return 400 with the allowed values; "
            + "a query running longer than 10 seconds returns 503 analytics_timeout.")]
    [ProducesResponseType(typeof(AnalyticsResultDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Query(
        [FromQuery, SwaggerParameter("Metric name from the catalogue, e.g. new_registrations, logins, account_role_mix.", Required = true)]
        string? metric,
        [FromQuery, SwaggerParameter("Comma-separated dimensions (max 2) allowed for the metric, e.g. month or week,role.")]
        string? by,
        [FromQuery, SwaggerParameter("Start date yyyy-MM-dd (UTC, inclusive). Default: to minus 89 days.")]
        string? from,
        [FromQuery, SwaggerParameter("End date or snapshot date yyyy-MM-dd (UTC, inclusive). Default: today. Range at most 366 days.")]
        string? to,
        [FromQuery, SwaggerParameter("Account role: User (or student), Admin, Teacher.")]
        string? role,
        [FromQuery, SwaggerParameter("Account status: Active, Inactive, Suspended.")]
        string? status,
        [FromQuery, SwaggerParameter("External sign-in provider: Google.")]
        string? provider,
        [FromQuery, SwaggerParameter("Maximum rows, 1-500. Default 100.")]
        int? limit,
        CancellationToken cancellationToken)
    {
        var result = await _sender.Send(
            new QueryAnalyticsQuery(metric, by, from, to, role, status, provider, limit),
            cancellationToken);

        return Ok(result);
    }
}
