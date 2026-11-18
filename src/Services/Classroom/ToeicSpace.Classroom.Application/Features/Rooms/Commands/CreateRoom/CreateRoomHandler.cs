using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Entities;
using ToeicSpace.Classroom.Domain.Enums;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Commands.CreateRoom;

public sealed class CreateRoomHandler(IFacilityRepository facilities, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<CreateRoomCommand, RoomDto>
{
    public async Task<RoomDto> Handle(CreateRoomCommand request, CancellationToken ct)
    {
        Room.Validate(request.Name, request.Capacity);
        var center = await facilities.CenterAsync(request.CenterId, ct)
                     ?? throw new ClassRuleException("center_not_found", "Center not found.", 404);
        if (center.Status != FacilityStatus.Active)
            throw new ClassRuleException("inactive_center", "Cannot add a room to an inactive center.", 409);
        var name = request.Name.Trim();
        if (await facilities.RoomNameExistsAsync(center.Id, name, null, ct))
            throw new ClassRuleException("duplicate_room", "Room name already exists in this center.", 409);
        var now = clock.GetUtcNow().UtcDateTime;
        var room = new Room
        {
            CenterId = center.Id, Center = center, Name = name, Capacity = request.Capacity,
            CreatedAt = now, UpdatedAt = now
        };
        facilities.Add(room);
        await unitOfWork.SaveChangesAsync(ct);
        return RoomDto.From(room);
    }
}