using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ToeicSpace.Classroom.Application.Common.Pagination;
using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Features.Rooms.Commands.CreateRoom;
using ToeicSpace.Classroom.Application.Features.Rooms.Commands.UpdateRoom;
using ToeicSpace.Classroom.Application.Features.Rooms.Commands.SetRoomStatus;
using ToeicSpace.Classroom.Application.Features.Rooms.Queries.GetRoom;
using ToeicSpace.Classroom.Application.Features.Rooms.Queries.ListRooms;
using ToeicSpace.Classroom.Domain.Enums;

namespace ToeicSpace.Classroom.API.Controllers;

[ApiController]
[Route("api/v1/rooms")]
[Authorize(Policy = "Admin")]
public sealed class RoomsController(ISender sender) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RoomDto>> Create(CreateRoomCommand command, CancellationToken ct)
    {
        var created = await sender.Send(command, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpGet]
    public Task<PagedResult<RoomDto>> List([FromQuery] Guid? centerId, [FromQuery] string? search,
        [FromQuery] FacilityStatus? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20,
        CancellationToken ct = default) =>
        sender.Send(new ListRoomsQuery(centerId, search, status, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public Task<RoomDto> Get(Guid id, CancellationToken ct) => sender.Send(new GetRoomQuery(id), ct);

    [HttpPut("{id:guid}")]
    public Task<RoomDto> Update(Guid id, UpdateRoomRequest body, CancellationToken ct) =>
        sender.Send(new UpdateRoomCommand(id, body.CenterId, body.Name, body.Capacity), ct);

    [HttpPost("{id:guid}/activate")]
    public Task<RoomDto> Activate(Guid id, CancellationToken ct) =>
        sender.Send(new SetRoomStatusCommand(id, FacilityStatus.Active), ct);

    [HttpPost("{id:guid}/deactivate")]
    public Task<RoomDto> Deactivate(Guid id, CancellationToken ct) =>
        sender.Send(new SetRoomStatusCommand(id, FacilityStatus.Inactive), ct);

    public sealed record UpdateRoomRequest(Guid CenterId, string Name, int Capacity);
}
