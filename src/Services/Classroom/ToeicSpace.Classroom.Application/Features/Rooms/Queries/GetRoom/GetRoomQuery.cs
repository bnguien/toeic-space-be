using ToeicSpace.Classroom.Application.DTOs.Facilities;

namespace ToeicSpace.Classroom.Application.Features.Rooms.Queries.GetRoom;

public sealed record GetRoomQuery(Guid Id) : IRequest<RoomDto>;
