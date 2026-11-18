using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Features.Centers.Commands.CreateCenter;
using ToeicSpace.Classroom.Application.Features.Centers.Commands.UpdateCenter;
using ToeicSpace.Classroom.Application.Features.Centers.Commands.SetCenterStatus;
using ToeicSpace.Classroom.Application.Features.Centers.Queries.GetCenter;
using ToeicSpace.Classroom.Application.Features.Centers.Queries.ListCenters;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/centers")]
[Authorize(Policy = "Admin")]
public sealed class CentersController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<CenterDto>> Create(CreateCenterCommand command, CancellationToken ct)
    {
        var created = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public Task<PagedResult<CenterDto>> List([FromQuery] string? search, [FromQuery] FacilityStatus? status,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 20, CancellationToken ct = default) =>
        sender.Send(new ListCentersQuery(search, status, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<CenterDto> Get(Guid id, CancellationToken ct) => sender.Send(new GetCenterQuery(id), ct);

    [HttpPut("{id:guid}")]
    public Task<CenterDto> Update(Guid id, UpdateCenterRequest body, CancellationToken ct) =>
        sender.Send(new UpdateCenterCommand(id, body.Name, body.Address, body.TimeZoneId), ct);

    [HttpPost("{id:guid}/activate")]
    public Task<CenterDto> Activate(Guid id, CancellationToken ct) =>
        sender.Send(new SetCenterStatusCommand(id, FacilityStatus.Active), ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<CenterDto> Deactivate(Guid id, CancellationToken ct) =>
        sender.Send(new SetCenterStatusCommand(id, FacilityStatus.Inactive), ct);

    public sealed record UpdateCenterRequest(string Name, string Address, string TimeZoneId);
}
