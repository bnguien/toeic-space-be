using ToeicSpace.Classroom.Application.DTOs.Facilities;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Commands.CreateRoom;

public sealed record CreateRoomCommand(Guid CenterId, string Name, int Capacity) : IRequest<RoomDto>;