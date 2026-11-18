using ToeicSpace.Classroom.Application.DTOs.Facilities;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Commands.UpdateRoom;

public sealed record UpdateRoomCommand(Guid Id, Guid CenterId, string Name, int Capacity) : IRequest<RoomDto>;
