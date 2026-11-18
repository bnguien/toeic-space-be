using ToeicSpace.Classroom.Application.DTOs.Facilities;
using ToeicSpace.Classroom.Application.Interfaces;
using ToeicSpace.Classroom.Application.Interfaces.Repositories;
using ToeicSpace.Classroom.Domain.Exceptions;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Commands.SetRoomStatus;

public sealed class SetRoomStatusHandler(IFacilityRepository facilities, IUnitOfWork unitOfWork, TimeProvider clock)
    : IRequestHandler<SetRoomStatusCommand, RoomDto>
{
    public async Task<RoomDto> Handle(SetRoomStatusCommand request, CancellationToken ct)
    {
        var room = await facilities.RoomAsync(request.Id, ct)
            ?? throw new ClassRuleException("room_not_found", "Room not found.", 404);
        room.Status = request.Status;
        room.UpdatedAt = clock.GetUtcNow().UtcDateTime;
        await unitOfWork.SaveChangesAsync(ct);
        return RoomDto.From(room);
    }
}
