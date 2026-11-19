using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Queries.GetRoom;

public sealed class GetRoomHandler(IFacilityRepository facilities) : IRequestHandler<GetRoomQuery, RoomDto>
{
    public async Task<RoomDto> Handle(GetRoomQuery request, CancellationToken ct) =>
        RoomDto.From(await facilities.RoomAsync(request.Id, ct)
            ?? throw new ClassRuleException("room_not_found", "Room not found.", 404));
}
