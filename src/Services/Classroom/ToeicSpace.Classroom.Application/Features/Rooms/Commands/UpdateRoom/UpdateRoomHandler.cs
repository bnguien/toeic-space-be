using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Commands.UpdateRoom;

public sealed class UpdateRoomHandler(IFacilityRepository facilities, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<UpdateRoomCommand, RoomDto>
{
    public async Task<RoomDto> Handle(UpdateRoomCommand request, CancellationToken ct)
    {
        Room.Validate(request.Name, request.Capacity);
        var room = await facilities.RoomAsync(request.Id, ct)
            ?? throw new ClassRuleException("room_not_found", "Room not found.", 404);
        if (room.CenterId != request.CenterId)
            throw new ClassRuleException("immutable_center", "A room cannot be moved between centers.", 409);
        var name = request.Name.Trim();
        if (name != room.Name && await facilities.RoomNameExistsAsync(room.CenterId, name, room.Id, ct))
            throw new ClassRuleException("duplicate_room", "Room name already exists in this center.", 409);
        if (request.Capacity < await facilities.MaxActiveEnrollmentCountForRoomAsync(room.Id, ct))
            throw new ClassRuleException("room_too_small", "Capacity is below active enrollment count in an assigned class.", 409);
        room.Name = name;
        room.Capacity = request.Capacity;
        room.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);
        return RoomDto.From(room);
    }
}
